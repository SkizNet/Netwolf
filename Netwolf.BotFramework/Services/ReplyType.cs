// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Netwolf.BotFramework.Services;

public enum ReplyType
{
    /// <summary>
    /// Replies via PRIVMSG to the channel if the command was sent in channel,
    /// or to the sender if the command was sent privately to the bot.
    /// </summary>
    PublicMessage,
    /// <summary>
    /// Replies via NOTICE to the channel if the command was sent in channel,
    /// or to the sender if the command was sent privately to the bot.
    /// </summary>
    PublicNotice,
    /// <summary>
    /// Replies via PRIVMSG privately to the sender, regardless of whether
    /// the command was sent in channel or privately.
    /// </summary>
    PrivateMessage,
    /// <summary>
    /// Replies via NOTICE privately to the sender, regardless of whether
    /// the command was sent in channel or privately.
    /// </summary>
    PrivateNotice
}
