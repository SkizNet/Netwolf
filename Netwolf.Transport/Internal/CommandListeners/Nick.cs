// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.Extensions.Logging;

using Netwolf.Transport.Events;
using Netwolf.Transport.Exceptions;
using Netwolf.Transport.IRC;

using System.Text.RegularExpressions;

namespace Netwolf.Transport.Internal.CommandListeners;

[CommandListener]
internal partial class Nick : ICommandListener
{
    [GeneratedRegex("^[^:$ ,*?!@][^ ,*?!@]*$")]
    private static partial Regex ValidNickRegex();

    private ILogger<INetwork> Logger { get; init; }

    public IReadOnlyCollection<string> CommandFilter => ["NICK"];

    public Nick(ILogger<INetwork> logger)
    {
        Logger = logger;
    }

    public void Execute(CommandEventArgs args)
    {
        // NICK <nickname>
        var info = args.Network.AsNetworkInfo();
        var command = args.Command;

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

            args.Network.UnsafeUpdateUser(user with
            {
                Nick = command.Args[0]
            });
        }
    }
}
