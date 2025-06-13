// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.IRC;

namespace Netwolf.PluginFramework.Loader;

/// <summary>
/// Dynamically load and unload plugins
/// </summary>
public interface IPluginLoader
{
    /// <summary>
    /// An enumerable of all loaded plugins.
    /// There is no guaranteed internal ordering to the enumerable.
    /// </summary>
    IEnumerable<PluginMetadata> LoadedPlugins { get; }

    /// <summary>
    /// Attempt to load a plugin.
    /// </summary>
    /// <param name="path">Path to the plugin's main file</param>
    /// <param name="metadata">
    /// The loaded plugin's metadata, or null if the plugin cannot be loaded.
    /// If the plugin is already loaded, this will contain the currently-loaded plugin's metadata.
    /// </param>
    /// <returns>Status of the load attempt</returns>
    PluginLoadStatus Load(string path, out PluginMetadata? metadata);

    /// <summary>
    /// Attempt to unload and then reload a plugin.
    /// </summary>
    /// <param name="pluginId">The ID of the plugin to reload</param>
    /// /// <param name="metadata">
    /// The reloaded plugin's metadata, or null if the plugin cannot be reloaded.
    /// This will be null if the reload operation fails for any reason, even if unloading fails.
    /// </param>
    /// <returns>Status of the reload attempt. If the reload fails, the plugin may be in either a loaded or unloaded state.</returns>
    PluginLoadStatus Reload(int pluginId, out PluginMetadata? metadata);

    /// <summary>
    /// Attempt to unload a plugin.
    /// </summary>
    /// <param name="pluginId">The ID of the plugin to unload</param>
    /// <returns>Status of the unload attempt</returns>
    PluginLoadStatus Unload(int pluginId);
}
