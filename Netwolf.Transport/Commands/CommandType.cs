﻿// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later


// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.Transport.Commands;

/// <summary>
/// Type (direction) of an <see cref="ICommand"/>
/// </summary>
public enum CommandType
{
    /// <summary>
    /// Command sent from the client to the server
    /// </summary>
    Client,
    /// <summary>
    /// Command sent from the server to the client
    /// </summary>
    Server,
    /// <summary>
    /// Command sent from a client to a bot
    /// </summary>
    Bot
}
