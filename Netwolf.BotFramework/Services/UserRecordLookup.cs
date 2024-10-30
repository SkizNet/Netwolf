// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Logging;

using Netwolf.BotFramework.Exceptions;
using Netwolf.BotFramework.State;
using Netwolf.Transport.Extensions;
using Netwolf.Transport.IRC;

using System.Collections.Concurrent;

namespace Netwolf.BotFramework.Services;

/// <summary>
/// Class to look up a ChannelRecord object given user details,
/// creating a new object if necessary.
/// </summary>
public class UserRecordLookup
{
    private readonly ILogger<UserRecordLookup> _logger;
    private readonly ConcurrentDictionary<string, UserRecord> _cache = [];
    // not volatile since this is expected to basically never change (just maybe on reconnect)
    // an ircd that sends a new mapping in 005 on a live connection multiple times in short succession
    // will just cause a bit of extra work in re-encoding things
    private int _caseMapping = 0;
    private CaseMapping CaseMapping => (CaseMapping)_caseMapping;

    public UserRecordLookup(ILogger<UserRecordLookup> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Retrieve an existing user, adding a new one if it doesn't exist
    /// </summary>
    /// <param name="nick">Nickname</param>
    /// <param name="ident">Ident, only used when adding a new user</param>
    /// <param name="host">Host, only used when adding a new user</param>
    /// <param name="account">Account, only used when adding a new user</param>
    /// <param name="realName">Real name (gecos), only used when adding a new user</param>
    /// <param name="isAway">Away status, only used when adding a new user</param>
    /// <returns></returns>
    internal UserRecord GetOrAddUser(string nick, string ident, string host, string? account = null, string realName = "", bool isAway = false)
    {
        return _cache.GetOrAdd(BotUtil.Casefold(nick, CaseMapping).DecodeUtf8(), _ => new UserRecord(nick, ident, host, account, realName, isAway));
    }

    /// <summary>
    /// Look up an existing record by nickname
    /// </summary>
    /// <param name="nick"></param>
    /// <returns>The record if it exists, or null if it doesn't.</returns>
    public UserRecord? GetUserByNick(string nick)
    {
        return _cache.GetValueOrDefault(BotUtil.Casefold(nick, CaseMapping).DecodeUtf8());
    }

    /// <summary>
    /// Look up existing users by account name
    /// </summary>
    /// <param name="account"></param>
    /// <returns></returns>
    public IEnumerable<UserRecord> GetUsersByAccount(string account)
    {
        ArgumentNullException.ThrowIfNull(account);

        // _cache.Values takes a snapshot so we don't need to worry about mutations while iterating
        foreach (var user in _cache.Values)
        {
            if (user.Account == account)
            {
                yield return user;
            }
        }
    }

    /// <summary>
    /// Retrieve all users known to the bot. This method is intended
    /// primarily for further refinement via LINQ to Objects queries
    /// when other built-in filter methods are not sufficient.
    /// </summary>
    /// <returns></returns>
    public IEnumerable<UserRecord> GetAllUsers()
    {
        return _cache.Values;
    }

    /// <summary>
    /// Renames a user record; can be used for casing changes
    /// in the display name or for entirely different names.
    /// No-ops if the old name doesn't exist.
    /// </summary>
    /// <param name="oldName"></param>
    /// <param name="newName"></param>
    internal void RenameUser(string oldName, string newName)
    {
        string oldFolded = BotUtil.Casefold(oldName, CaseMapping).DecodeUtf8();
        string newFolded = BotUtil.Casefold(newName, CaseMapping).DecodeUtf8();
        if (!_cache.TryGetValue(oldFolded, out var user))
        {
            // old channel no longer exists, so no-op
            return;
        }

        // update the nickname (could be a casing change, or could be something more)
        user.Nick = newName;

        // if we're totally changing the name, need to also update _cache keys
        if (oldFolded != newFolded)
        {
            RenameUserInternal(user, oldFolded, newFolded);
        }
    }

    /// <summary>
    /// Sets the case mapping to use when folding nicknames.
    /// If this changes, all existing names will be updated,
    /// which can be a lengthy operation.
    /// </summary>
    /// <param name="caseMapping"></param>
    internal void SetCaseMapping(CaseMapping caseMapping)
    {
        int newMapping = (int)caseMapping;
        int oldMapping = Interlocked.Exchange(ref _caseMapping, newMapping);
        if (newMapping == oldMapping)
        {
            // nothing to do here
            return;
        }

        // otherwise we now have to re-casefold EVERY key in _cache
        // it's ok to get a key snapshot here as we've already swapped CaseMapping above,
        // so any keys added in the meantime will use the new mapping
        foreach (string oldKey in _cache.Keys)
        {
            string newKey = BotUtil.Casefold(oldKey, caseMapping).DecodeUtf8();
            if (newKey == oldKey)
            {
                // nothing changed in the casefolding for this user (yay!)
                continue;
            }

            if (_cache.TryGetValue(oldKey, out var user))
            {
                RenameUserInternal(user, oldKey, newKey);
            }
        }
    }

    /// <summary>
    /// Renames the channel key in _cache. Must be given already-casefolded keys.
    /// </summary>
    /// <param name="channel"></param>
    /// <param name="oldFolded"></param>
    /// <param name="newFolded"></param>
    private void RenameUserInternal(UserRecord user, string oldFolded, string newFolded)
    {
        UserRecord previous = _cache.GetOrAdd(newFolded, user);

        // if we already had a different user with the new name, that's bad
        // this indicates our internal state got corrupted somehow or maybe the ircd is screwing with us
        if (user != previous)
        {
            _logger.LogError("Attempting to rename {Old} to {New} but a user with the new nickname already exists.", oldFolded, newFolded);
            throw new BadStateException("Attemping to rename a user but a user with the new nickname already exists.");
        }

        // now that we're updated, purge the old name
        // if this fails that means the old name was effectively already purged,
        // due to being overwritten by another thread, so there is no need to retry
        _cache.TryRemove(new(oldFolded, user));
    }

    /// <summary>
    /// Removes a UserRecord from the cache
    /// </summary>
    /// <param name="user"></param>
    internal void RemoveUser(UserRecord user)
    {
        _cache.TryRemove(new(BotUtil.Casefold(user.Nick, CaseMapping).DecodeUtf8(), user));
    }

    /// <summary>
    /// Must only be called upon disconnect
    /// </summary>
    internal void ClearAllUsers()
    {
        _cache.Clear();
    }

    /// <summary>
    /// Must only be called on the bot's UserRecord after calling ClearAllUsers() above
    /// </summary>
    /// <param name="user"></param>
    /// <exception cref="InvalidOperationException"></exception>
    internal void AddExistingUser(UserRecord user)
    {
        if (!_cache.TryAdd(BotUtil.Casefold(user.Nick, CaseMapping).DecodeUtf8(), user))
        {
            throw new InvalidOperationException($"Trying to add a user {user.Nick} that already exists in cache. This is a bug in Netwolf.BotFramework.");
        }
    }
}
