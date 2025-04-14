// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.PluginFramework;

/// <summary>
/// Exposes methods that a plugin can use to interact with its host application.
/// </summary>
/// <remarks>
/// This API surface is carefully designed to avoid exposing symbols from the plugin
/// assembly into the base AssemblyLoadContext. When that is unavoidable (e.g. callbacks),
/// they are maintained by the plugin host layer and not further exposed to the rest
/// of the framework so that we only need to look into/touch one place during an unload event.
/// </remarks>
public interface IPluginHost
{
    // TODO: add ways to add commands and hook into server events
    // also work in concert with DI "Plugin*Provider" services for registration
    IPluginHook HookServerCommand();

    IPluginHook HookClientCommand();
}
