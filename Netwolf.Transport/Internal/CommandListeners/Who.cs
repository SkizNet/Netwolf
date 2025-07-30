// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.Events;
using Netwolf.Transport.State;

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Netwolf.Transport.Internal.CommandListeners;

[CommandListener]
internal class Who : ICommandListener
{
    public IReadOnlyCollection<string> CommandFilter => ["352"];

    [SuppressMessage("Style", "IDE0301:Simplify collection initialization",
        Justification = "ImmutableHashSet.Empty is more semantically meaningful")]
    public void Execute(CommandEventArgs args)
    {
        // RPL_WHOREPLY (352) <client> <channel> <username> <host> <server> <nick> <flags> :<hopcount> <realname>
        var info = args.Network.AsNetworkInfo();
        var command = args.Command;

        var hopReal = command.Args[7].Split(' ', 2);
        string realName = hopReal.Length > 1 ? hopReal[1] : string.Empty;
        if (info.GetUserByNick(command.Args[5]) is UserRecord user)
        {
            // existing user; update info
            user = user with
            {
                Ident = command.Args[2],
                Host = command.Args[3],
                RealName = realName,
                IsAway = command.Args[6][0] == 'G',
            };
        }
        else
        {
            // previously unknown user
            user = new UserRecord(
                Guid.NewGuid(),
                command.Args[5],
                command.Args[2],
                command.Args[3],
                null,
                command.Args[6][0] == 'G',
                realName,
                ImmutableHashSet<char>.Empty,
                ImmutableDictionary<Guid, string>.Empty);
        }

        // channel being null is ok here since /who nick returns an arbitrary channel or potentially a '*'
        ChannelRecord? channel = command.Args[1] == "*" ? null : info.GetChannel(command.Args[1]);

        // channel isn't known and this user didn't share any other channels with us
        if (user.Id != info.ClientId && channel == null && user.Channels.Count == 0)
        {
            return;
        }

        // if channel is known, update prefixes
        if (channel != null)
        {
            // determine prefix
            int prefixStart = (command.Args[6].Length == 1 || command.Args[6][1] != '*') ? 1 : 2;
            string prefix = string.Concat(command.Args[6][prefixStart..].TakeWhile(info.ChannelPrefixSymbols.Contains));

            args.Network.UnsafeUpdateUser(user with { Channels = user.Channels.SetItem(channel.Id, prefix) });
        }
        else
        {
            args.Network.UnsafeUpdateUser(user);
        }
    }
}
