// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.Events;
using Netwolf.Transport.State;

namespace Netwolf.Transport.Internal.CommandListeners;

[CommandListener]
internal class Topic : ICommandListener
{
    public IReadOnlyCollection<string> CommandFilter => ["332"];

    public void Execute(CommandEventArgs args)
    {
        // RPL_TOPIC (332) <client> <channel> :<topic>
        // Note: we ignore the topic setter
        var info = args.Network.AsNetworkInfo();
        var command = args.Command;

        if (info.GetChannel(command.Args[1]) is ChannelRecord channel)
        {
            args.Network.UnsafeUpdateChannel(channel with
            {
                Topic = command.Args[2]
            });
        }
    }
}
