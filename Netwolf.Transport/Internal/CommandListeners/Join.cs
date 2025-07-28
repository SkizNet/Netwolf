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
internal class Join : ICommandListener
{
    private ILogger<INetwork> Logger { get; init; }

    public IReadOnlyCollection<string> CommandFilter => ["JOIN"];

    public Join(ILogger<INetwork> logger)
    {
        Logger = logger;
    }

    public Task ExecuteAsync(CommandEventArgs args)
    {
        // regular join: JOIN <channel>
        // extended-join: JOIN <channel> <account> :<gecos>
        var info = args.Network.AsNetworkInfo();
        var command = args.Command;

        if (command.Source == null)
        {
            Logger.LogWarning("Protocol violation: JOIN message lacks a source");
            return Task.CompletedTask;
        }

        var channel = info.GetChannel(command.Args[0]);
        var (nick, ident, host) = IrcUtil.SplitHostmask(command.Source);
        // don't blow up if the ircd gave us garbage
        if (string.IsNullOrEmpty(nick) || string.IsNullOrEmpty(ident) || string.IsNullOrEmpty(host))
        {
            Logger.LogWarning("Protocol violation: JOIN message source is not a full nick!user@host");
            return Task.CompletedTask;
        }

        var user = info.GetUserByNick(nick);

        if (channel == null)
        {
            if (user?.Id == info.ClientId)
            {
                // if we joined a new channel, add it to state
                channel = new ChannelRecord(
                    Guid.NewGuid(),
                    command.Args[0],
                    string.Empty,
                    ImmutableDictionary<char, string?>.Empty,
                    ImmutableDictionary<Guid, string>.Empty);
            }
            else
            {
                // someone other than us joining a channel we aren't aware of
                Logger.LogWarning("Potential state corruption detected: Received JOIN message for another user on {Channel} but it does not exist in state", command.Args[0]);
                return Task.CompletedTask;
            }
        }

        string? account = null;
        string realName = string.Empty;
        if (info.TryGetEnabledCap("extended-join", out _))
        {
            account = command.Args[1] != "*" ? command.Args[1] : null;
            realName = command.Args[2];

            if (user != null)
            {
                user = user with
                {
                    Account = account,
                    RealName = realName
                };
            }
        }

        user ??= new UserRecord(
            Guid.NewGuid(),
            nick,
            ident,
            host,
            account,
            false,
            realName,
            ImmutableHashSet<char>.Empty,
            ImmutableDictionary<Guid, string>.Empty);

        args.Network.UnsafeUpdateUser(user with
        {
            Channels = user.Channels.SetItem(channel.Id, string.Empty)
        });

        args.Network.UnsafeUpdateChannel(channel with
        {
            Users = channel.Users.SetItem(user.Id, string.Empty)
        });

        return Task.CompletedTask;
    }
}
