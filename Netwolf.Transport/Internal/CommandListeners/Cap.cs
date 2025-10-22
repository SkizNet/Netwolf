// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.Extensions.Logging;

using Netwolf.Transport.Commands;
using Netwolf.Transport.Events;
using Netwolf.Transport.Extensions;
using Netwolf.Transport.IRC;

using System.Reactive.Linq;
using System.Threading;

namespace Netwolf.Transport.Internal.CommandListeners;

[CommandListener]
internal sealed class Cap : IAsyncCommandListener, IDisposable
{
    private record CapFinished(INetwork Network, ICommand Command, CancellationToken Token);

    public IReadOnlyCollection<string> CommandFilter => ["CAP", "410"];

    private ILogger<INetwork> Logger { get; init; }

    private CommandListenerRegistry Registry { get; init; }

    private Dictionary<INetwork, HashSet<string>> PendingCaps { get; init; } = new(ReferenceEqualityComparer.Instance);

    private IDisposable? _saslSubscription;
    private IDisposable? _finishedSubscription;

    /// <summary>
    /// CAPs that are always enabled if supported by the server, because we support them in this library
    /// </summary>
    private static readonly HashSet<string> DefaultCaps =
    [
        "account-notify",
        "away-notify",
        "batch",
        "cap-notify",
        "chghost",
        "draft/channel-rename",
        "draft/multiline",
        "extended-join",
        "message-ids",
        "message-tags",
        "multi-prefix",
        "server-time",
        "setname",
        "userhost-in-names",
    ];

    public Cap(ILogger<INetwork> logger, CommandListenerRegistry registry, INetworkRegistry networkRegistry)
    {
        Logger = logger;
        Registry = registry;

        _saslSubscription = registry.CommandListenerEvents
            .OfType<SaslEventArgs>()
            .SubscribeAsync(HandleSaslFinished);

        _finishedSubscription = registry.CommandListenerEvents
            .OfType<CapFinished>()
            .SubscribeAsync(DoCapReq);

        networkRegistry.NetworkCreated += (sender, network) =>
        {
            PendingCaps[network] = [];
            network.NetworkConnecting += (sender, args) => PendingCaps[args.Network].Clear();
        };

        networkRegistry.NetworkDestroyed += (sender, network) => PendingCaps.Remove(network);
    }

    public async Task ExecuteAsync(CommandEventArgs args)
    {
        args.Token.ThrowIfCancellationRequested();

        // CAP nickname subcommand args...
        // 410 nickname subcommand :Invalid CAP command
        var command = args.Command;
        var network = args.Network;

        if (command.Verb == "410" && !network.IsConnected)
        {
            // CAP command failed, bail out
            // although if it's saying our CAP END failed (broken ircd), don't cause an infinite loop
            Logger.LogWarning("Server rejected CAP {Command} (potentially broken ircd)", command.Args[1]);
            if (command.Args[1] != "END")
            {
                await network.UnsafeSendRawAsync("CAP END", args.Token);
            }

            return;
        }

        // we handle each command independently rather than relying on DeferredCommand chaining so that
        // user-issued CAP commands can be handled here as well
        switch (command.Args[1])
        {
            case "LS":
            case "NEW":
                HandleNewCap(network, command, args.Token);
                break;
            case "ACK":
            case "LIST":
                await HandleEnabledCap(network, command, args.Token);
                break;
            case "DEL":
                HandleDisabledCap(network, command, args.Token);
                break;
            case "NAK":
                await HandleRejectedCap(network, command, args.Token);
                break;
            default:
                // not something we recognize; log but otherwise ignore
                Logger.LogInformation("Received unrecognized CAP {Command} from server (potentially broken ircd)", command.Args[1]);
                break;
        }
    }

    private void HandleNewCap(INetwork network, ICommand command, CancellationToken cancellationToken)
    {
        string caps;
        bool final = false;

        // request supported CAPs; might be multi-line so don't act until we get everything
        if (command.Args[1] == "LS" && command.Args[2] == "*")
        {
            // multiline
            caps = command.Args[3];
        }
        else
        {
            // final LS
            caps = command.Args[2];
            final = true;
        }

        foreach (var cap in caps.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string key = cap;
            string? value = null;

            if (cap.Contains('='))
            {
                var bits = cap.Split('=', 2);
                key = bits[0];
                value = bits[1];
            }

            Registry.EmitEvent(new CapEventArgs(network, key, value, command.Args[1], cancellationToken));
        }

        // only make requests for LS pre-registration so we don't re-request if a user issues their own CAP LS,
        // but always make requests for CAP NEW if we receive one
        if (final && (command.Args[1] == "NEW" || !network.IsConnected))
        {
            // The registry events run on a separate thread, so schedule the actual CAP REQ on that same thread
            // to ensure that network state is fully up-to-date
            Registry.EmitEvent(new CapFinished(network, command, cancellationToken));
        }
    }

    private async Task DoCapReq(CapFinished args)
    {
        var (network, command, cancellationToken) = args;
        var state = network.AsNetworkInfo();

        foreach (var (key, value) in state.GetAllSupportedCaps())
        {
            if (DefaultCaps.Contains(key) || await network.ShouldEnableCapAsync(key, value, command.Args[1], cancellationToken))
            {
                PendingCaps[network].Add(key);
            }
        }

        // ShouldEnableCap callbacks may have disconnected us from the network or cancelled our token
        cancellationToken.ThrowIfCancellationRequested();
        if (network.Server is null)
        {
            return;
        }

        // handle extremely large cap sets by breaking into multiple CAP REQ commands;
        // we want to ensure the server's response (ACK or NAK) fits within 512 bytes with protocol overhead
        // :server CAP nick ACK :data\r\n -- 14 bytes of overhead (leaving 498), plus nicklen, plus serverlen
        // we reserve another 64 bytes just in case there is other unexpected overhead. better to send an extra
        // CAP REQ than to be rejected because the server reply is longer than we anticipated it'd be
        // Note: don't use MaxLength here since we're still pre-registration and haven't received ISUPPORT
        int maxBytes = 434 - (state.Nick?.Length ?? 1) - (command.Source?.Length ?? 0);
        int consumedBytes = 0;
        List<string> param = [];

        foreach (var token in PendingCaps[network])
        {
            if (consumedBytes + token.Length > maxBytes)
            {
                await network.UnsafeSendRawAsync($"CAP REQ :{string.Join(" ", param)}", cancellationToken);
                consumedBytes = 0;
                param.Clear();
            }

            param.Add(token);
            consumedBytes += token.Length;
        }

        if (param.Count > 0)
        {
            await network.UnsafeSendRawAsync($"CAP REQ :{string.Join(" ", param)}", cancellationToken);
        }
        else if (!network.IsConnected)
        {
            // we don't support any of the server's caps, so end cap negotiation here
            await network.UnsafeSendRawAsync("CAP END", cancellationToken);
        }
    }

    private void HandleDisabledCap(INetwork network, ICommand command, CancellationToken cancellationToken)
    {
        // mark CAPs as disabled client-side if applicable; don't send CAP END in any event here
        var removedCaps = command.Args[2].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var allCaps = network.AsNetworkInfo().GetAllSupportedCaps();

        foreach (var cap in removedCaps)
        {
            Registry.EmitEvent(
                new CapEventArgs(network, cap, allCaps.GetValueOrDefault(cap), command.Args[1], cancellationToken));
        }
    }

    private async Task HandleEnabledCap(INetwork network, ICommand command, CancellationToken cancellationToken)
    {
        // mark CAPs as enabled client-side, then finish cap negotiation if this was an ACK (not a LIST)
        var newCaps = command.Args[2].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var allCaps = network.AsNetworkInfo().GetAllSupportedCaps();

        foreach (var cap in newCaps)
        {
            Registry.EmitEvent(
                new CapEventArgs(network, cap, allCaps.GetValueOrDefault(cap), command.Args[1], cancellationToken));

            // mark the cap as no longer pending since this is confirmation it is enabled
            // with exception of SASL because we need to suspend CAP END until that finishes
            if (cap != "sasl")
            {
                PendingCaps[network].Remove(cap);
            }
        }

        if (command.Args[1] == "ACK" && PendingCaps[network].Count == 0 && !network.IsConnected)
        {
            await network.UnsafeSendRawAsync("CAP END", cancellationToken);
        }
    }

    private async Task HandleRejectedCap(INetwork network, ICommand command, CancellationToken cancellationToken)
    {
        var rejectedCaps = command.Args[2].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var allCaps = network.AsNetworkInfo().GetAllSupportedCaps();

        foreach (var cap in rejectedCaps)
        {
            Registry.EmitEvent(
                new CapEventArgs(network, cap, allCaps.GetValueOrDefault(cap), command.Args[1], cancellationToken));

            // mark the cap as no longer pending since this is confirmation it is not supported server-side
            PendingCaps[network].Remove(cap);
        }

        if (PendingCaps[network].Count == 0 && !network.IsConnected)
        {
            await network.UnsafeSendRawAsync("CAP END", cancellationToken);
        }
    }

    private async Task HandleSaslFinished(SaslEventArgs args)
    {
        var network = args.Network;
        if (PendingCaps[network].Remove("sasl") && PendingCaps[network].Count == 0 && !network.IsConnected)
        {
            await network.UnsafeSendRawAsync("CAP END", args.Token);
        }
    }

    void IDisposable.Dispose()
    {
        _saslSubscription?.Dispose();
        _finishedSubscription?.Dispose();
        _saslSubscription = null;
        _finishedSubscription = null;
    }
}
