// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.Events;
using Netwolf.Transport.IRC;
using Netwolf.Transport.State;

namespace Netwolf.Transport.Internal.CommandListeners;

[CommandListener]
internal class Away : ICommandListener
{
    public IReadOnlyCollection<string> CommandFilter => ["AWAY", "301", "305", "306"];

    public void Execute(CommandEventArgs args)
    {
        var info = args.Network.AsNetworkInfo();
        var command = args.Command;

        switch (command.Verb)
        {
            case "AWAY":
                // AWAY [:<message>]
                {
                    if (IrcUtil.TryExtractUserFromSource(command, info, out var user))
                    {
                        args.Network.UnsafeUpdateUser(user with { IsAway = command.Args.Count > 0 });
                    }
                }

                break;
            case "301":
                // RPL_AWAY <client> <nick> :<message>
                {
                    if (info.GetUserByNick(command.Args[1]) is UserRecord user)
                    {
                        args.Network.UnsafeUpdateUser(user with { IsAway = true });
                    }
                }

                break;
            case "305":
                // RPL_UNAWAY <client> :You are no longer marked as being away
                args.Network.UnsafeUpdateUser(info.Self with { IsAway = false });
                break;
            case "306":
                // RPL_NOWAWAY <client> :You have been marked as being away
                args.Network.UnsafeUpdateUser(info.Self with { IsAway = true });
                break;
        }
    }
}
