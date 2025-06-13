// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.PluginFramework;

/// <summary>
/// Represents the result of a plugin operation
/// </summary>
[Flags]
public enum PluginResult: int
{
    /// <summary>
    /// Continue with plugin execution followed by default logic.
    /// </summary>
    Continue = 0,
    /// <summary>
    /// Halts further processing of this command by other plugins, but still runs the default logic.
    /// Not all hook APIs support suppression.
    /// </summary>
    SuppressPlugins = 1,
    /// <summary>
    /// Continues processing of this command by other plugins, but prevents the default logic from running.
    /// Default logic will not run regardless of the return value from later plugin hooks.
    /// Not all hook APIs support suppression.
    /// </summary>
    SuppressDefault = 2,
    /// <summary>
    /// Halts further processing of this command by other plugins and prevents the default logic from running.
    /// Not all hook APIs support suppression.
    /// </summary>
    SuppressAll = 3,
}
