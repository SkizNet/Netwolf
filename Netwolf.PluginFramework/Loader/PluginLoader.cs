// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.PluginFramework.Loader;

internal class PluginLoader : IPluginLoader
{
    private int _nextPluginId = 1;
    private readonly Dictionary<int, PluginLoadContext> _loadedPlugins = [];

    public IEnumerable<PluginMetadata> LoadedPlugins => from kvp in _loadedPlugins
                                                        select new PluginMetadata(
                                                            kvp.Key,
                                                            kvp.Value.Plugin.Name,
                                                            kvp.Value.Plugin.Description,
                                                            kvp.Value.Plugin.Version,
                                                            kvp.Value.Path);

    public PluginLoadStatus Load(string path, out PluginMetadata? metadata)
    {
        throw new NotImplementedException();
    }

    public PluginLoadStatus Reload(int pluginId)
    {
        throw new NotImplementedException();
    }

    public PluginLoadStatus Unload(int pluginId)
    {
        throw new NotImplementedException();
    }
}
