// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.Events;
using Netwolf.Transport.IRC;

namespace Netwolf.Transport.Internal.CommandListeners;

[CommandListener]
internal class Account : ICommandListener
{
    public IReadOnlyCollection<string> CommandFilter => ["ACCOUNT"];

    public void Execute(CommandEventArgs args)
    {
        // ACCOUNT <accountname>
        var info = args.Network.AsNetworkInfo();
        var command = args.Command;

        if (IrcUtil.TryExtractUserFromSource(command, info, out var user))
        {
            args.Network.UnsafeUpdateUser(user with
            {
                Account = command.Args[0] == "*" ? null : command.Args[0]
            });
        }
    }
}
