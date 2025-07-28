// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.Extensions.Logging;

using Netwolf.Transport.Events;
using Netwolf.Transport.IRC;
using Netwolf.Transport.State;

namespace Netwolf.Transport.Internal.CommandListeners;

[CommandListener]
internal class Kick : ICommandListener
{
    private ILogger<INetwork> Logger { get; init; }

    public IReadOnlyCollection<string> CommandFilter => ["KICK"];

    public Kick(ILogger<INetwork> logger)
    {
        Logger = logger;
    }

    public Task ExecuteAsync(CommandEventArgs args)
    {
        // KICK <channel> <user> [:<comment>]
        var info = args.Network.AsNetworkInfo();
        var command = args.Command;

        if (command.Args[1].Contains(','))
        {
            Logger.LogWarning("Protocol violation: KICK message contains multiple nicks");
            return Task.CompletedTask;
        }

        if (info.GetChannel(command.Args[0]) is not ChannelRecord channel)
        {
            Logger.LogWarning("Potential state corruption detected: Received KICK message for {Channel} but it does not exist in state", command.Args[0]);
            return Task.CompletedTask;
        }

        if (info.GetUserByNick(command.Args[1]) is not UserRecord user)
        {
            Logger.LogWarning("Potential state corruption detected: Received KICK message for {Nick} but they do not exist in state", command.Args[1]);
            return Task.CompletedTask;
        }

        args.Network.UnsafeUpdateChannel(channel with
        {
            Users = channel.Users.Remove(user.Id)
        });

        return Task.CompletedTask;
    }
}
