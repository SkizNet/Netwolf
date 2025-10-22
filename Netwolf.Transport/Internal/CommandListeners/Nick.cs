// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Netwolf.Transport.Commands;
using Netwolf.Transport.Events;
using Netwolf.Transport.Exceptions;
using Netwolf.Transport.IRC;

using System.Text.RegularExpressions;
using System.Threading;

namespace Netwolf.Transport.Internal.CommandListeners;

[CommandListener]
internal partial class Nick : IAsyncCommandListener
{
    [GeneratedRegex("^[^:$ ,*?!@][^ ,*?!@]*$")]
    private static partial Regex ValidNickRegex();

    private ILogger<INetwork> Logger { get; init; }

    public IReadOnlyCollection<string> CommandFilter => ["NICK", "001", "432", "433"];

    public Nick(ILogger<INetwork> logger)
    {
        Logger = logger;
    }

    public async Task ExecuteAsync(CommandEventArgs args)
    {
        switch (args.Command.Verb)
        {
            case "NICK":
                HandleNick(args.Network, args.Command);
                break;
            case "001": // RPL_WELCOME
                HandleWelcome(args.Network, args.Command);
                break;
            case "432": // ERR_ERRONEUSNICKNAME
            case "433": // ERR_NICKNAMEINUSE
                await HandleBadNick(args.Network, args.Command, args.Token);
                break;
        }
    }

    private void HandleNick(INetwork network, ICommand command)
    {
        // NICK <nickname>
        var info = network.AsNetworkInfo();

        if (!ValidNickRegex().IsMatch(command.Args[0]))
        {
            Logger.LogWarning("Protocol violation: nickname contains illegal characters");
            return;
        }

        if (info.ChannelTypes.Contains(command.Args[0][0]) || info.ChannelPrefixSymbols.Contains(command.Args[0][0]))
        {
            Logger.LogWarning("Protocol violation: nickname begins with a channel or status prefix");
            return;
        }

        if (IrcUtil.TryExtractUserFromSource(command, info, out var user))
        {
            if (info.GetUserByNick(command.Args[0]) is not null && !IrcUtil.IrcEquals(user.Nick, command.Args[0], info.CaseMapping))
            {
                throw new BadStateException($"Nick collision detected; attempting to rename {user.Nick} to {command.Args[0]} but the new nick already exists in state");
            }

            network.UnsafeUpdateUser(user with
            {
                Nick = command.Args[0]
            });
        }
    }

    private static void HandleWelcome(INetwork network, ICommand command)
    {
        // 001 <nick> :Welcome to the Internet Relay Chat Network <nick>!<user>@<host>
        var info = network.AsNetworkInfo();
        network.UnsafeUpdateUser(info.Self with
        {
            Nick = command.Args[0]
        });
    }

    private async Task HandleBadNick(INetwork network, ICommand command, CancellationToken cancellationToken)
    {
        // 432 <nick> :Erroneous Nickname
        // 433 <nick> :Nickname is already in use

        // only handle these during pre-registration; don't automatically attempt different nicks
        // if the user issued a manual NICK command
        if (!network.IsConnected)
        {
            return;
        }

        string attempted = command.Args[0];
        string secondary = network.Options.SecondaryNick ?? $"{network.Options.PrimaryNick}_";
        if (attempted == network.Options.PrimaryNick)
        {
            await network.UnsafeSendRawAsync($"NICK {secondary}", cancellationToken);
        }
        else if (attempted == secondary)
        {
            // both taken? abort
            Logger.LogError("Server rejected both primary and secondary nicks. Aborting connection.");
            await network.DisconnectAsync();
        }
    }
}
