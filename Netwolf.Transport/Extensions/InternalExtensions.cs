// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.Commands;

namespace Netwolf.Transport.Extensions;

internal static class InternalExtensions
{
    private static readonly HashSet<string> MESSAGE_TYPES = ["PRIVMSG", "NOTICE", "TAGMSG", "CPRIVMSG", "CNOTICE"];

    /// <summary>
    /// Retrieves the target of a message (PRIVMSG, NOTICE, or TAGMSG).
    /// </summary>
    /// <param name="command">Command to retrieve the target from</param>
    /// <returns>The message target, or <c>null</c> if <paramref name="command"/> is not a PRIVMSG, NOTICE, or TAGMSG command.</returns>
    public static string? GetMessageTarget(this ICommand command)
    {
        if (MESSAGE_TYPES.Contains(command.Verb))
        {
            return command.Args[0];
        }

        return null;
    }
}
