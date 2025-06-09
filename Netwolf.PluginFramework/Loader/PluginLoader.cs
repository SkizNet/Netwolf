// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.PluginFramework.Commands;
using Netwolf.PluginFramework.Exceptions;
using Netwolf.Transport.IRC;

using System.Reflection;

namespace Netwolf.PluginFramework.Loader;

internal class PluginLoader : IPluginLoader
{
    private record PluginInfo(IPlugin Plugin, PluginLoadContext Context, PluginHost Host);

    private int _nextPluginId = 0;
    private readonly Dictionary<int, PluginInfo> _loadedPlugins = [];

    public IEnumerable<PluginMetadata> LoadedPlugins => from kvp in _loadedPlugins
                                                        select new PluginMetadata(
                                                            kvp.Key,
                                                            kvp.Value.Plugin.Name,
                                                            kvp.Value.Plugin.Description,
                                                            kvp.Value.Plugin.Version,
                                                            kvp.Value.Context.Path);

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

        try
        {
            var assembly = context.LoadFromAssemblyPath(path);
            if (assembly.GetCustomAttribute<PluginClassAttribute>() is not PluginClassAttribute pluginClass)
            {
                // TODO: log message
                context.Unload();
                return PluginLoadStatus.NotAPlugin;
            }

            if (!pluginClass.PluginType.IsAssignableTo(typeof(IPlugin)))
            {
                // TODO: log message
                context.Unload();
                return PluginLoadStatus.NotAPlugin;
            }

            // we have a plugin! grab metadata and initialize it
            // Activator.CreateInstance returns null only for Nullable<T> types, which the plugin is guaranteed not to be
            var pluginRef = (IPlugin)Activator.CreateInstance(pluginClass.PluginType)!;
            var pluginHost = new PluginHost(this, pluginId);
            _loadedPlugins[pluginId] = new(pluginRef, context, pluginHost);
            metadata = new(pluginId, pluginRef.Name, pluginRef.Description, pluginRef.Version, context.Path);
            pluginRef.Initialize(pluginHost);

            return PluginLoadStatus.Success;
        }
        catch (Exception ex)
        {
            // TODO: log exception details (take an ILogger in the constructor since this is a DI service)
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

    public PluginLoadStatus Reload(int pluginId)
    {
        throw new NotImplementedException();
    }

    public PluginLoadStatus Unload(int pluginId)
    {
        throw new NotImplementedException();
    }

    public IDisposable AddNetwork(IObservable<ICommand> commandStream, ICommandDispatcher commandDispatcher)
    {
        throw new NotImplementedException();
    }
}
