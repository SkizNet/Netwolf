// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.Transport.IRC;

/// <summary>
/// Type of a message to send
/// </summary>
public enum MessageType
{
    /// <summary>
    /// Regular message (corresponds to PRIVMSG or CPRIVMSG)
    /// </summary>
    Message,
    /// <summary>
    /// Notice (corresponds to NOTICE or CNOTICE)
    /// </summary>
    Notice
}
