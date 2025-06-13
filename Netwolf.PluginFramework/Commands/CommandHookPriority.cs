// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.PluginFramework.Commands;

/// <summary>
/// Defines the ordering in which hooks are run.
/// When multiple hooks exist with the same priority, execution depends on the registry implementation.
/// For the default command hook registry, they are executed in the order they are registered.
/// </summary>
public enum CommandHookPriority
{
    Highest,
    High,
    Normal,
    Low,
    Lowest,
}
