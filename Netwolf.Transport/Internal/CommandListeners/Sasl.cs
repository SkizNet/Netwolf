// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Netwolf.Transport.Commands;
using Netwolf.Transport.Events;
using Netwolf.Transport.IRC;
using Netwolf.Transport.Sasl;
using Netwolf.Transport.State;

using System.Security.Authentication.ExtendedProtection;
using System.Text;

namespace Netwolf.Transport.Internal.CommandListeners;

[CommandListener]
internal class Sasl : IAsyncCommandListener, INetworkInitialization
{
    /// <summary>
    /// Maximum length of SaslBuffer (64 KiB base64-encoded, 48 KiB after decoding).
    /// We abort SASL if the server sends more data than this
    /// </summary>
    private const int SASL_BUFFER_MAX_LENGTH = 65536;

    private ILogger<INetwork> Logger { get; init; }

    private CommandListenerRegistry Registry { get; init; }

    private ISaslMechanismFactory SaslMechanismFactory { get; init; }

    /// <summary>
    /// SASL mechanisms that are supported by both the server and us.
    /// We will try these in order of most to least secure.
    /// If the server doesn't support CAP version 302, this will only be the mechanisms
    /// selected by the client and doesn't necessarily speak to server support.
    /// Mechanisms will be removed from this set as they are tried so that we don't re-try them.
    /// </summary>
    private Dictionary<INetwork, HashSet<string>> SaslMechs { get; init; } = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// SASL mechanism that is currently in use, or <c>null</c> if SASL isn't being attempted or failed
    /// </summary>
    private Dictionary<INetwork, ISaslMechanism?> SelectedSaslMech { get; init; } = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Buffer for SASL data in case it spans multiple lines
    /// </summary>
    private Dictionary<INetwork, StringBuilder> SaslBuffer { get; init; } = new(ReferenceEqualityComparer.Instance);

    public IReadOnlyCollection<string> CommandFilter => [
        "AUTHENTICATE",
        "900", // RPL_LOGGEDIN
        "901", // RPL_LOGGEDOUT
        "902", // ERR_NICKLOCKED
        "903", // RPL_SASLSUCCESS
        "904", // ERR_SASLFAIL
        "905", // ERR_SASLTOOLONG
        "906", // ERR_SASLABORTED
        "907", // ERR_SASLALREADY
        "908", // RPL_SASLMECHS
        ];

    public Sasl(ILogger<INetwork> logger, CommandListenerRegistry registry, ISaslMechanismFactory saslMechanismFactory)
    {
        Logger = logger;
        Registry = registry;
        SaslMechanismFactory = saslMechanismFactory;
    }

    public void InitializeForNetwork(INetwork network)
    {
        network.NetworkConnecting += InitSaslState;
        network.NetworkDisconnected += ClearSaslState;
        network.CapFilter += EnableSasl;
    }

    private void InitSaslState(object? sender, NetworkEventArgs args)
    {
        SaslMechs[args.Network] = [];
        SelectedSaslMech[args.Network] = null;
        SaslBuffer[args.Network] = new StringBuilder();
    }

    private void ClearSaslState(object? sender, NetworkEventArgs args)
    {
        SaslMechs.Remove(args.Network);
        SelectedSaslMech.Remove(args.Network);
        SaslBuffer.Remove(args.Network);
    }

    private async ValueTask<bool> EnableSasl(object? sender, CapEventArgs args)
    {
        // this is called for every capability; ensure we only look at "sasl"
        if (args.CapName != "sasl")
        {
            return false;
        }

        // save some typing further down
        var options = args.Network.Options;
        var state = args.Network.AsNetworkInfo();

        // do we have SASL credentials defined client-side?
        if (!options.UseSasl && (options.AccountPassword != null || options.AccountCertificateFile != null))
        {
            return false;
        }

        // are we already logged in? If so, don't do SASL again
        if (state.Account != null)
        {
            return false;
        }

        // ensure we're connected and have a defined server
        if (args.Network.Server is not ServerRecord server)
        {
            return false;
        }

        // negotiate SASL
        HashSet<string> supportedSaslTypes = [.. SaslMechanismFactory.GetSupportedMechanisms(options, server)];

        if (args.CapValue != null)
        {
            supportedSaslTypes.IntersectWith(args.CapValue.Split(','));
        }

        supportedSaslTypes.ExceptWith(options.DisabledSaslMechs);

        if (args.CapValue == null || supportedSaslTypes.Count != 0)
        {
            // Request SASL
            SaslMechs[args.Network] = supportedSaslTypes;
            return true;
        }
        else if (supportedSaslTypes.Count == 0 && options.AbortOnSaslFailure)
        {
            Logger.LogError("Server and client have no SASL mechanisms in common; aborting connection");
            await args.Network.DisconnectAsync();
        }

        return false;
    }

    public async Task ExecuteAsync(CommandEventArgs args)
    {
        var state = args.Network.AsNetworkInfo();

        switch (args.Command.Verb)
        {
            case "AUTHENTICATE":
                await OnAuthenticate(args.Network, args.Command, args.Token);
                break;
            case "900":
                // successful SASL, record our account name
                args.Network.UnsafeUpdateUser(state.Self with { Account = args.Command.Args[2] });
                break;
            case "901":
                // we got logged out
                args.Network.UnsafeUpdateUser(state.Self with { Account = null });
                break;
            case "903":
            case "907":
                await FinishSasl(args.Network, true, args.Token);
                break;
            case "904":
            case "905":
                // SASL failed, retry with next mech
                await AttemptSasl(args.Network, args.Token);
                break;
            case "902":
            case "906":
                // SASL failed, don't retry
                await FinishSasl(args.Network, false, args.Token);
                break;
            case "908":
                // failed, but will also get a 904, simply update supported mechs
                await UpdateMechs(args.Network, args.Command);
                break;
        }
    }

    private async Task AttemptSasl(INetwork network, CancellationToken cancellationToken)
    {
        var options = network.Options;
        if (network.Server is not ServerRecord server)
        {
            // not connected, can't do SASL
            return;
        }

        foreach (var mech in SaslMechanismFactory.GetSupportedMechanisms(options, server))
        {
            if (SaslMechs[network].Contains(mech))
            {
                SelectedSaslMech[network]?.Dispose();
                var mechInstance = SaslMechanismFactory.CreateMechanism(mech, options);
                SelectedSaslMech[network] = mechInstance;

                if (mechInstance.SupportsChannelBinding && network is Network concreteNetwork)
                {
                    // Connection.GetChannelBinding returns null for an unsupported binding type (e.g. Unique on TLS 1.3+)
                    var uniqueData = concreteNetwork.Connection.GetChannelBinding(ChannelBindingKind.Unique);
                    var endpointData = concreteNetwork.Connection.GetChannelBinding(ChannelBindingKind.Endpoint);

                    if (
                        !mechInstance.SetChannelBindingData(ChannelBindingKind.Unique, uniqueData)
                        && !mechInstance.SetChannelBindingData(ChannelBindingKind.Endpoint, endpointData))
                    {
                        // we want binding but it's not supported; skip this mech
                        mechInstance.Dispose();
                        SelectedSaslMech[network] = null;
                        SaslMechs[network].Remove(mech);
                        continue;
                    }
                }

                SaslMechs[network].Remove(mech);
                await network.UnsafeSendRawAsync($"AUTHENTICATE {mech}", cancellationToken);
                return;
            }
        }

        // no more mechs in common
        SelectedSaslMech[network]?.Dispose();
        SelectedSaslMech[network] = null;

        if (options.AbortOnSaslFailure && !network.IsConnected)
        {
            Logger.LogError("All SASL mechanisms supported by both server and client failed, aborting connection.");
            await network.DisconnectAsync();
            return;
        }

        // Let CAP handler know that we're done with SASL so it can send CAP END if needed
        Registry.EmitEvent(new SaslEventArgs(network, null, cancellationToken));
    }

    private async Task FinishSasl(INetwork network, bool success, CancellationToken cancellationToken)
    {
        var mech = SelectedSaslMech[network]?.Name;
        SelectedSaslMech[network]?.Dispose();
        SelectedSaslMech[network] = null;

        if (!success && network.Options.AbortOnSaslFailure && !network.IsConnected)
        {
            Logger.LogWarning("SASL failed with an unrecoverable error; aborting connection");
            await network.DisconnectAsync();
            return;
        }

        // Let CAP handler know that we're done with SASL so it can send CAP END if needed
        Registry.EmitEvent(new SaslEventArgs(network, mech, cancellationToken));
    }

    private async Task UpdateMechs(INetwork network, ICommand command)
    {
        SaslMechs[network].IntersectWith(command.Args[1].Split(','));
        if (SaslMechs[network].Count == 0 && network.Options.AbortOnSaslFailure && !network.IsConnected)
        {
            Logger.LogError("Server and client have no SASL mechanisms in common; aborting connection");
            await network.DisconnectAsync();
        }
    }

    private async Task OnAuthenticate(INetwork network, ICommand command, CancellationToken cancellationToken)
    {
        bool done = false;
        var buffer = SaslBuffer[network];

        if (SelectedSaslMech[network] is not ISaslMechanism mech)
        {
            // unexpected AUTHENTICATE command; ignore
            return;
        }

        if (command.Args[0] == "+")
        {
            // have full server response, do whatever we need with it
            done = true;
        }
        else
        {
            buffer.Append(command.Args[0]);

            // received the last line of data
            if (command.Args[0].Length < 400)
            {
                done = true;
            }

            // prevent DOS from malicious servers by making buffer expand forever
            // if the buffer grows too large, we abort SASL
            if (buffer.Length > SASL_BUFFER_MAX_LENGTH)
            {
                buffer.Clear();
                mech.Dispose();
                SelectedSaslMech[network] = null;
                await network.UnsafeSendRawAsync("AUTHENTICATE *", cancellationToken);
                return;
            }
        }

        if (done)
        {
            byte[] data;
            if (buffer.Length > 0)
            {
                data = Convert.FromBase64String(buffer.ToString());
                buffer.Clear();
            }
            else
            {
                data = [];
            }

            bool success = mech.Authenticate(data, out var responseBytes);

            if (!success)
            {
                // abort SASL
                mech.Dispose();
                SelectedSaslMech[network] = null;
                await network.UnsafeSendRawAsync("AUTHENTICATE *", cancellationToken);
            }
            else
            {
                // send response
                if (responseBytes.Length == 0)
                {
                    await network.UnsafeSendRawAsync("AUTHENTICATE +", cancellationToken);
                }
                else
                {
                    var response = Convert.ToBase64String(responseBytes);
                    int start = 0;

                    do
                    {
                        int end = Math.Min(start + 400, response.Length);
                        await network.UnsafeSendRawAsync($"AUTHENTICATE {response[start..end]}", cancellationToken);
                        start = end;
                    } while (start < response.Length);

                    if (response.Length % 400 == 0)
                    {
                        // if we sent exactly 400 bytes in the last line, send a blank line to let server know we're done
                        await network.UnsafeSendRawAsync("AUTHENTICATE +", cancellationToken);
                    }
                }
            }
        }
    }
}
