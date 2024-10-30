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
/// Class to look up a ChannelRecord object given a channel name,
/// creating a new object if necessary.
/// </summary>
public class ChannelRecordLookup
{
    private readonly ILogger<ChannelRecordLookup> _logger;
    private readonly ConcurrentDictionary<string, ChannelRecord> _cache = [];
    // not volatile since this is expected to basically never change (just maybe on reconnect)
    // an ircd that sends a new mapping in 005 on a live connection multiple times in short succession
    // will just cause a bit of extra work in re-encoding things
    private int _caseMapping = 0;
    private CaseMapping CaseMapping => (CaseMapping)_caseMapping;

    public ChannelRecordLookup(ILogger<ChannelRecordLookup> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Look up an existing record
    /// </summary>
    /// <param name="name">Channel name</param>
    /// <returns>The record if found in cache, otherwise null</returns>
    public ChannelRecord? GetChannel(string name)
    {
        return _cache.GetValueOrDefault(BotUtil.Casefold(name, CaseMapping).DecodeUtf8());
    }

    /// <summary>
    /// Look up a record, creating a new one if it doesn't exist.
    /// No validation is performed on channel prefixes.
    /// </summary>
    /// <param name="name">Name to look up. If a channel doesn't exist, the casing of this name will be used as the display casing for the channel</param>
    /// <param name="topic">Channel topic; only used if creating a new ChannelRecord</param>
    /// <param name="modes">Channel modes; only used if creating a new ChannelRecord</param>
    /// <returns></returns>
    internal ChannelRecord GetOrAddChannel(string name)
    {
        return _cache.GetOrAdd(BotUtil.Casefold(name, CaseMapping).DecodeUtf8(), _ => new ChannelRecord(name));
    }

    /// <summary>
    /// Renames a channel record; can be used for casing changes
    /// in the display name or for entirely different names.
    /// No validation is performed on channel prefixes.
    /// No-ops if the old name doesn't exist.
    /// </summary>
    /// <param name="oldName"></param>
    /// <param name="newName"></param>
    internal void RenameChannel(string oldName, string newName)
    {
        string oldFolded = BotUtil.Casefold(oldName, CaseMapping).DecodeUtf8();
        string newFolded = BotUtil.Casefold(newName, CaseMapping).DecodeUtf8();
        if (!_cache.TryGetValue(oldFolded, out var channel))
        {
            // old channel no longer exists, so no-op
            return;
        }

        // update the channel name (could be a casing change, or could be something more)
        channel.Name = newName;

        // if we're totally changing the name, need to also update _cache keys
        if (oldFolded != newFolded)
        {
            RenameChannelInternal(channel, oldFolded, newFolded);
        }
    }

    /// <summary>
    /// Sets the case mapping to use when folding channel names.
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
                // nothing changed in the casefolding for this channel (yay!)
                continue;
            }

            if (_cache.TryGetValue(oldKey, out var channel))
            {
                RenameChannelInternal(channel, oldKey, newKey);
            }
        }
    }

    /// <summary>
    /// Renames the channel key in _cache. Must be given already-casefolded keys.
    /// </summary>
    /// <param name="channel"></param>
    /// <param name="oldFolded"></param>
    /// <param name="newFolded"></param>
    private void RenameChannelInternal(ChannelRecord channel, string oldFolded, string newFolded)
    {
        ChannelRecord previous = _cache.GetOrAdd(newFolded, channel);

        // if we already had a different channel with the new name, that's bad
        // this indicates our internal state got corrupted somehow or maybe the ircd is screwing with us
        if (channel != previous)
        {
            _logger.LogError("Attempting to rename {Old} to {New} but a channel with the new name already exists.", oldFolded, newFolded);
            throw new BadStateException("Attemping to rename a channel but a channel with the new name already exists.");
        }

        // now that we're updated, purge the old name
        // if this fails that means the old name was effectively already purged,
        // due to being overwritten by another thread, so there is no need to retry
        _cache.TryRemove(new(oldFolded, channel));
    }

    /// <summary>
    /// Removes a ChannelRecord from the cache
    /// </summary>
    /// <param name="user"></param>
    internal void RemoveChannel(ChannelRecord channel)
    {
        _cache.TryRemove(new(BotUtil.Casefold(channel.Name, CaseMapping).DecodeUtf8(), channel));
    }

    /// <summary>
    /// Must only be called upon disconnect
    /// </summary>
    internal void ClearAllChannels()
    {
        _cache.Clear();
    }
}
