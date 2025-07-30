// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.Events;
using Netwolf.Transport.Exceptions;
using Netwolf.Transport.IRC;
using Netwolf.Transport.State;

namespace Netwolf.Transport.Internal.CommandListeners;

[CommandListener]
internal class Rename : ICommandListener
{
    public IReadOnlyCollection<string> CommandFilter => ["RENAME"];

    public void Execute(CommandEventArgs args)
    {
        // RENAME <old_channel> <new_channel> :<reason>
        var info = args.Network.AsNetworkInfo();
        var command = args.Command;

        if (info.GetChannel(command.Args[0]) is ChannelRecord channel && info.ChannelTypes.Contains(command.Args[1][0]))
        {
            if (info.GetChannel(command.Args[1]) is not null && !IrcUtil.IrcEquals(channel.Name, command.Args[1], info.CaseMapping))
            {
                throw new BadStateException($"Channel collision detected; attempting to rename {channel.Name} to {command.Args[1]} but the new channel already exists in state");
            }

            args.Network.UnsafeUpdateChannel(channel with
            {
                Name = command.Args[1]
            });
        }
    }
}
