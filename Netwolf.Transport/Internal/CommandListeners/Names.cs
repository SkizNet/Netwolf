// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.Extensions.Logging;

using Netwolf.Transport.Commands;
using Netwolf.Transport.Events;
using Netwolf.Transport.IRC;
using Netwolf.Transport.State;

using System.Collections.Immutable;

namespace Netwolf.Transport.Internal.CommandListeners;

[CommandListener]
internal class Names : ICommandListener
{
    private ILogger<INetwork> Logger { get; init; }

    public IReadOnlyCollection<string> CommandFilter => ["353"];

    public Names(ILogger<INetwork> logger)
    {
        Logger = logger;
    }

    public void Execute(CommandEventArgs args)
    {
        // RPL_NAMREPLY (353) <client> <symbol> <channel> :[prefix]<nick>{ [prefix]<nick>}
        // Note: symbol is ignored, but in theory we could (un)set +s or +p for channel based on it
        var info = args.Network.AsNetworkInfo();
        var command = args.Command;

        // if userhost-in-names isn't enabled we only get nicknames here, which means UserRecords will have empty string idents/hosts,
        // which is a corner case that downstream users shouldn't need to deal with. Better for them to just fail a record lookup until
        // they issue a WHO for the channel.
        if (info.GetChannel(command.Args[2]) is not ChannelRecord channel || !info.TryGetEnabledCap("userhost-in-names", out _))
        {
            return;
        }

        // make a copy since the underlying property on State recomputes the value on each access
        string prefixSymbols = info.ChannelPrefixSymbols;
        foreach (var prefixedNick in command.Args[3].Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string prefix = string.Concat(prefixedNick.TakeWhile(prefixSymbols.Contains));
            // per above, userhost-in-names is enabled so we get all 3 components
            var (nick, ident, host) = IrcUtil.SplitHostmask(prefixedNick[prefix.Length..]);
            if (string.IsNullOrEmpty(nick) || string.IsNullOrEmpty(ident) || string.IsNullOrEmpty(host))
            {
                Logger.LogWarning("Protocol violation: NAMES does not contain a full nick!user@host despite userhost-in-names being negotiated");
                break;
            }

            var user = info.GetUserByNick(nick)
                ?? new UserRecord(
                    Guid.NewGuid(),
                    nick,
                    ident,
                    host,
                    null,
                    false,
                    string.Empty,
                    ImmutableHashSet<char>.Empty,
                    ImmutableDictionary<Guid, string>.Empty);

            args.Network.UnsafeUpdateUser(user with
            {
                Channels = user.Channels.SetItem(channel.Id, prefix)
            });
        }
    }
}
