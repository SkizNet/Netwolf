// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.PluginFramework.Loader;

/// <summary>
/// Shim class between a plugin and the application.
/// IPluginHost defines the public API that a plugin is able to use to perform interactions.
/// This class *must not* hold a reference to the IPlugin itself in any long-duration lifetime,
/// as otherwise it may become impossible to unload/collect plugins (= memory leaks).
/// </summary>
internal class PluginHost : IPluginHost
{
    private PluginLoader PluginLoader { get; init; }

    private int PluginId { get; init; }

    public PluginHost(PluginLoader pluginLoader, int pluginId)
    {
        PluginLoader = pluginLoader;
        PluginId = pluginId;
    }
}
