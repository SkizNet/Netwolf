// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.Extensions.Logging;

using Netwolf.PluginFramework.Commands;
using Netwolf.PluginFramework.Exceptions;

using System.Reflection;

namespace Netwolf.PluginFramework.Loader;

internal class PluginLoader : IPluginLoader
{
    private record PluginInfo(IPlugin Plugin, PluginLoadContext Context, PluginHost Host);

    private ILogger<IPluginLoader> Logger { get; init; }
    private ICommandHookRegistry HookRegistry { get; init; }

    private int _nextPluginId = 0;
    private readonly Dictionary<int, PluginInfo> _loadedPlugins = [];

    public IEnumerable<PluginMetadata> LoadedPlugins => from kvp in _loadedPlugins
                                                        select new PluginMetadata(
                                                            kvp.Key,
                                                            kvp.Value.Plugin.Name,
                                                            kvp.Value.Plugin.Description,
                                                            kvp.Value.Plugin.Version,
                                                            kvp.Value.Context.Path);

    public PluginLoader(ILogger<IPluginLoader> logger, ICommandHookRegistry hookRegistry)
    {
        Logger = logger;
        HookRegistry = hookRegistry;
    }

    public PluginLoadStatus Load(string path, out PluginMetadata? metadata)
    {
        metadata = null;

        // TODO: use the 2-arg version of GetFullPath with a base path pointing to the expected default plugin directory
        var absolute = Path.GetFullPath(path);
        var fileName = Path.GetFileName(absolute);

        if (fileName == string.Empty)
        {
            return PluginLoadStatus.FileNotFound;
        }

        var existing = LoadedPlugins.Where(p => p.Path == absolute).FirstOrDefault();
        if (existing != null)
        {
            metadata = existing;
            return PluginLoadStatus.AlreadyLoaded;
        }

        var pluginId = Interlocked.Increment(ref _nextPluginId);
        var context = new PluginLoadContext($"Netwolf.Plugin{pluginId} [{fileName}]", path);
        PluginHost? pluginHost = null;

        try
        {
            var assembly = context.LoadFromAssemblyPath(path);
            if (assembly.GetCustomAttribute<PluginClassAttribute>() is not PluginClassAttribute pluginClass)
            {
                Logger.LogWarning("The plugin path {Path} does not contain a valid plugin (missing PluginClassAttribute on assembly)", path);
                context.Unload();
                return PluginLoadStatus.NotAPlugin;
            }

            if (!pluginClass.PluginType.IsAssignableTo(typeof(IPlugin)))
            {
                Logger.LogWarning("The plugin path {Path} does not contain a valid plugin (plugin class does not implement IPlugin)", path);
                context.Unload();
                return PluginLoadStatus.NotAPlugin;
            }

            // we have a plugin! grab metadata and initialize it
            // Activator.CreateInstance returns null only for Nullable<T> types, which the plugin is guaranteed not to be
            var pluginRef = (IPlugin)Activator.CreateInstance(pluginClass.PluginType)!;
            pluginHost = new PluginHost(this, HookRegistry, pluginId);
            _loadedPlugins[pluginId] = new(pluginRef, context, pluginHost);
            metadata = new(pluginId, pluginRef.Name, pluginRef.Description, pluginRef.Version, context.Path);
            pluginRef.Initialize(pluginHost);

            return PluginLoadStatus.Success;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Encountered an exception when loading plugin at {Path}", path);
            pluginHost?.Dispose();
            context.Unload();

            return ex switch
            {
                UnloadedFrameworkException => PluginLoadStatus.FrameworkMismatch,
                FrameworkMismatchException => PluginLoadStatus.VersionMismatch,
                FileNotFoundException => PluginLoadStatus.FileNotFound,
                FileLoadException or BadImageFormatException => PluginLoadStatus.InvalidAssembly,
                _ => PluginLoadStatus.UnknownError,
            };
        }
    }

    public PluginLoadStatus Reload(int pluginId, out PluginMetadata? metadata)
    {
        metadata = null;

        if (!_loadedPlugins.TryGetValue(pluginId, out var info))
        {
            Logger.LogWarning("Attempted to reload plugin ID {PluginId}, but no such plugin is loaded", pluginId);
            return PluginLoadStatus.NotLoaded;
        }

        var path = info.Context.Path;
        var status = Unload(pluginId);
        if (status != PluginLoadStatus.Success)
        {
            return status;
        }

        return Load(path, out metadata);
    }

    public PluginLoadStatus Unload(int pluginId)
    {
        if (!_loadedPlugins.TryGetValue(pluginId, out var info))
        {
            Logger.LogWarning("Attempted to unload plugin ID {PluginId}, but no such plugin is loaded", pluginId);
            return PluginLoadStatus.NotLoaded;
        }

        // unregister all hooks, etc.
        info.Host.Dispose();

        // remove refs to assembly load context so it can be collected
        _loadedPlugins.Remove(pluginId);
        info.Context.Unload();
        return PluginLoadStatus.Success;
    }
}
