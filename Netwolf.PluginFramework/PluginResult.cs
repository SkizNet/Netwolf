// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.PluginFramework;

/// <summary>
/// Represents the result of a plugin operation
/// </summary>
public enum PluginResult: int
{
    /// <summary>
    /// Continue with plugin execution followed by default logic.
    /// </summary>
    Continue = 0,
    /// <summary>
    /// Halts further processing of this hook by other plugins and prevents the default logic from running.
    /// Not all hook APIs support suppression.
    /// </summary>
    Suppress = 1
}
