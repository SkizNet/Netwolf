// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.Events;
using Netwolf.Transport.IRC;

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Netwolf.Transport.Internal.CommandListeners;

[CommandListener]
internal class UserModeIs : ICommandListener
{
    public IReadOnlyCollection<string> CommandFilter => ["221"];

    [SuppressMessage("Style", "IDE0305:Simplify collection initialization",
        Justification = "ToImmutableHashSet() is more semantically meaningful")]
    public void Execute(CommandEventArgs args)
    {
        // RPL_UMODEIS <client> <user modes>
        var info = args.Network.AsNetworkInfo();
        var command = args.Command;

        if (IrcUtil.TryExtractUserFromSource(command, info, out var user))
        {
            args.Network.UnsafeUpdateUser(user with
            {
                Modes = command.Args[1].ToImmutableHashSet()
            });
        }
    }
}
