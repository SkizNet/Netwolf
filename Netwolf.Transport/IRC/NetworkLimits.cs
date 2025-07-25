// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.Transport.IRC;

/// <summary>
/// Holds the various limits for a connection.
/// </summary>
/// <param name="LineLength">Maximum length (in bytes) of a network line excluding tags</param>
/// <param name="ClientTagLength">Maximum length (in bytes) of client tags</param>
/// <param name="ServerTagLength">Maximum length (in bytes) of all tags (client + server)</param>
public record NetworkLimits(int LineLength = 512, int ClientTagLength = 4096, int ServerTagLength = 8191);
