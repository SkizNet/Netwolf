// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.PluginFramework;

/// <summary>
/// Represents a hook that a plugin has with the base framework.
/// Storing IPluginHook instances is only necessary if you plan on removing the hook within the plugin
/// outside of an unload event. Otherwise, it is safe to discard these objects.
/// </summary>
public interface IPluginHook
{
    /// <summary>
    /// Removes this hook, making it no longer active.
    /// It is safe to call this method multiple times; subsequent calls no-op.
    /// </summary>
    void Unhook();
}
