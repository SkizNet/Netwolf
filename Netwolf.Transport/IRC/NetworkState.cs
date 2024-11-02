// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.State;

using System.Collections.Immutable;

namespace Netwolf.Transport.IRC;

/// <summary>
/// Holds the state of the client's connection
/// </summary>
public sealed record NetworkState(
    string Name,
    Guid ClientId,
    CaseMapping CaseMapping,
    ImmutableDictionary<Guid, UserRecord> Users,
    ImmutableDictionary<Guid, ChannelRecord> Channels,
    ImmutableDictionary<string, Guid> Lookup,
    ImmutableDictionary<string, string?> SupportedCaps,
    ImmutableHashSet<string> EnabledCaps,
    ImmutableDictionary<ISupportToken, string?> ISupport
    ) : INetworkInfo
{
    public string Nick => Users[ClientId].Nick;
    
    public string Ident => Users[ClientId].Ident;
    
    public string Host => Users[ClientId].Host;

    public string? Account => Users[ClientId].Account;

    public string RealName => Users[ClientId].RealName;

    public bool IsAway => Users[ClientId].IsAway;

    public ImmutableHashSet<char> UserModes => Users[ClientId].Modes;

    IReadOnlyDictionary<ChannelRecord, string> INetworkInfo.Channels => GetChannelsForUser(ClientId);

    public bool TryGetEnabledCap(string cap, out string? value)
    {
        value = SupportedCaps.GetValueOrDefault(cap);
        return EnabledCaps.Contains(cap);
    }

    public bool TryGetISupport(ISupportToken token, out string? value)
    {
        return ISupport.TryGetValue(token, out value);
    }

    public string? GetISupportOrDefault(ISupportToken token, string? defaultValue = null)
    {
        return ISupport.GetValueOrDefault(token, defaultValue);
    }

    public IReadOnlyDictionary<UserRecord, string> GetUsersInChannel(ChannelRecord channel) => GetUsersInChannel(channel.Id);

    public IReadOnlyDictionary<UserRecord, string> GetUsersInChannel(Guid channelId) =>
        Channels.GetValueOrDefault(channelId)?.Users.ToDictionary(x => Users[x.Key], x => x.Value) ?? [];

    public IReadOnlyDictionary<ChannelRecord, string> GetChannelsForUser(UserRecord user) => GetChannelsForUser(user.Id);

    public IReadOnlyDictionary<ChannelRecord, string> GetChannelsForUser(Guid userId)
        => Users.GetValueOrDefault(userId)?.Channels.ToDictionary(x => Channels[x.Key], x => x.Value) ?? [];

    public UserRecord? GetUserByNick(string nick) =>
        Lookup.TryGetValue(IrcUtil.Casefold(nick, CaseMapping), out var key) && Users.TryGetValue(key, out var user)
            ? user
            : null;

    public IEnumerable<UserRecord> GetUsersByAccount(string account) =>
        Users.Select(x => x.Value).Where(x => IrcUtil.IrcEquals(x.Account, account, CaseMapping));

    public IEnumerable<UserRecord> GetAllUsers() => Users.Select(x => x.Value);

    public ChannelRecord? GetChannel(string name) =>
        Lookup.TryGetValue(IrcUtil.Casefold(name, CaseMapping), out var key) && Channels.TryGetValue(key, out var channel)
            ? channel
            : null;
}
