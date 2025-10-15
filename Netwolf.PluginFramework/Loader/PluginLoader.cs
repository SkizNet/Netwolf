// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.Extensions.Logging;

using Netwolf.PluginFramework.Commands;
using Netwolf.PluginFramework.Exceptions;
using Netwolf.PluginFramework.Extensions;

using System.Reflection;
using System.Runtime.CompilerServices;

namespace Netwolf.PluginFramework.Loader;

internal class PluginLoader : IPluginLoader, IDisposable
{
    private ILogger<IPluginLoader> Logger { get; init; }
    private ICommandHookRegistry HookRegistry { get; init; }

    private int _nextPluginId = 0;
    private volatile bool _disposed = false;
    // Everything that deals with this field needs to have MethodImplOptions.NoInlining
    // otherwise inlining optimizations can prevent the ALC from being collected
    private readonly Dictionary<int, PluginInfo> _loadedPlugins = [];

    public IEnumerable<PluginMetadata> LoadedPlugins
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        get
        {
            // return an eagerly-generated copy so that _loadedPlugins doesn't stay around
            // potentially long-term or forever (and therefore blocks collection of the ALC)
            return [.. from kvp in _loadedPlugins
                   select new PluginMetadata(kvp.Key, kvp.Value)];
        }
    }

    public PluginLoader(ILogger<IPluginLoader> logger, ICommandHookRegistry hookRegistry)
    {
        Logger = logger;
        HookRegistry = hookRegistry;
    }

    /// <summary>
    /// Internal method for unit testing. This should have no callers outside of unit tests;
    /// use <see cref="LoadedPlugins"/> instead and operated on the returned <see cref="PluginMetadata"/>.
    /// </summary>
    /// <param name="pluginId">Plugin ID</param>
    /// <returns>PluginInfo record</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal PluginInfo GetPluginInfoForUnitTesting(int pluginId) => _loadedPlugins[pluginId];

    [MethodImpl(MethodImplOptions.NoInlining)]
    public PluginLoadStatus Load(string path, out PluginMetadata? metadata)
    {
        // Once a Dispose is initiated, do not allow any further loads
        ObjectDisposedException.ThrowIf(_disposed, nameof(PluginLoader));
        metadata = null;

        // TODO: use the 2-arg version of GetFullPath with a base path pointing to the expected default plugin directory
        var absolute = Path.GetFullPath(path);
        var fileName = Path.GetFileName(absolute);

        if (fileName == string.Empty)
        {
            Logger.LogWarning("The filename portion of {Path} is empty", absolute);
            return PluginLoadStatus.FileNotFound;
        }

        var existing = LoadedPlugins.Where(p => p.Path == absolute).FirstOrDefault();
        if (existing != null)
        {
            Logger.LogDebug("The plugin located at {Path} is already loaded", absolute);
            metadata = existing;
            return PluginLoadStatus.AlreadyLoaded;
        }

        var pluginId = Interlocked.Increment(ref _nextPluginId);
        var context = new PluginLoadContext($"Netwolf.Plugin{pluginId} [{fileName}]", absolute);
        PluginHost? pluginHost = null;
        IPlugin? pluginRef = null;

        Logger.LogDebug("Loading plugin from path {Path} with ID {PluginId}", absolute, pluginId);

        try
        {
            var assembly = context.LoadFromAssemblyPath(absolute);
            if (assembly.GetCustomAttribute<PluginClassAttribute>() is not PluginClassAttribute pluginClass)
            {
                Logger.LogWarning("The plugin path {Path} does not contain a valid plugin (missing PluginClassAttribute on assembly)", absolute);
                context.Unload();
                return PluginLoadStatus.NotAPlugin;
            }

            if (!pluginClass.PluginType.IsAssignableTo(typeof(IPlugin)))
            {
                Logger.LogWarning("The plugin path {Path} does not contain a valid plugin (plugin class does not implement IPlugin)", absolute);
                context.Unload();
                return PluginLoadStatus.NotAPlugin;
            }

            // we have a plugin! grab metadata and initialize it
            // Activator.CreateInstance returns null only for Nullable<T> types, which the plugin is guaranteed not to be
            pluginRef = (IPlugin)Activator.CreateInstance(pluginClass.PluginType)!;
            pluginHost = new PluginHost(HookRegistry, pluginRef, pluginId);
            pluginRef.Initialize(pluginHost);
            PluginInfo info = new(pluginRef, context, pluginHost);
            _loadedPlugins[pluginId] = info;
            metadata = new(pluginId, info);

            return PluginLoadStatus.Success;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Encountered an exception when loading plugin at {Path}", absolute);
            (pluginRef as IDisposable)?.SafeDispose(Logger);
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    public PluginLoadStatus Reload(int pluginId, out PluginMetadata? metadata)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(PluginLoader));
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    public PluginLoadStatus Unload(int pluginId)
    {
        // Unload() is called during Dispose and is allowed to be called while disposing the object
        // (or even afterwards since it'll just log warnings about plugins not being loaded)

        if (!_loadedPlugins.Remove(pluginId, out var info))
        {
            Logger.LogWarning("Attempted to unload plugin ID {PluginId}, but no such plugin is loaded", pluginId);
            return PluginLoadStatus.NotLoaded;
        }

        Logger.LogDebug("Unloading plugin ID {PluginId} from path {Path}", pluginId, info.Context.Path);

        // unregister all hooks, etc.
        info.Host.Dispose();

        // dispose the underlying plugin if it's disposable
        (info.Plugin as IDisposable)?.SafeDispose(Logger);

        // remove refs to assembly load context so it can be collected
        info.Context.Unload();
        return PluginLoadStatus.Success;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _disposed = true;

            if (disposing)
            {
                Logger.LogDebug("Disposing PluginLoader and unloading all loaded plugins");

                while (_loadedPlugins.Count > 0)
                {
                    Unload(_loadedPlugins.First().Key);
                }
            }
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
