// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.Events;
using Netwolf.Transport.IRC;

namespace Netwolf.Transport.Internal.CommandListeners;

[CommandListener]
internal class ChgHost : ICommandListener
{
    public IReadOnlyCollection<string> CommandFilter => ["CHGHOST"];

    public void Execute(CommandEventArgs args)
    {
        // CHGHOST <new_user> <new_host>
        var info = args.Network.AsNetworkInfo();
        var command = args.Command;

        if (IrcUtil.TryExtractUserFromSource(command, info, out var user))
        {
            args.Network.UnsafeUpdateUser(user with
            {
                Ident = command.Args[0],
                Host = command.Args[1],
            });
        }
    }
}
