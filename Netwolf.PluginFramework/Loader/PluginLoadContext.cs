// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.PluginFramework.Exceptions;

using System.Reflection;
using System.Runtime.Loader;

namespace Netwolf.PluginFramework.Loader;

internal class PluginLoadContext : AssemblyLoadContext
{
    private AssemblyDependencyResolver Resolver { get; init; }

    internal string Path { get; init; }

    /// <summary>
    /// Creates a new PluginLoadContext for plugin type isolation.
    /// </summary>
    /// <param name="name">Plugin name</param>
    /// <param name="path">
    /// Absolute path to the main plugin file.
    /// Plugin dependencies must be in the same directory as this file.
    /// </param>
    public PluginLoadContext(string name, string path)
        : base(name, isCollectible: true)
    {
        ArgumentNullException.ThrowIfNull(name);
        Path = path;
        Resolver = new(path);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // All Netwolf assemblies are shared and the version in the default ALC is used
        // Forward compatibility is guaranteed for patch versions, but not major or minor
        // Backward compatibility is guaranteed for minor or patch versions, but not major
        // Netwolf frameworks not loaded by the base project are unavailable for use
        if (assemblyName.Name?.StartsWith("Netwolf.") == true)
        {
            var existing = Default.Assemblies.FirstOrDefault(a => a.GetName().Name == assemblyName.Name)
                ?? throw new UnloadedFrameworkException(Name!, assemblyName);

            if (assemblyName.Version is Version requestedVersion)
            {
                var loadedVersion = existing.GetName().Version!;
                if (loadedVersion.Major != requestedVersion.Major || requestedVersion.Minor > loadedVersion.Minor)
                {
                    throw new FrameworkMismatchException(Name!, assemblyName, existing.GetName());
                }
            }

            return existing;
        }

        // All other types load a file present in the same directory as the plugin if one exists,
        // and falls back to default load logic if not
        if (Resolver.ResolveAssemblyToPath(assemblyName) is string assemblyPath)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        return null;
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        if (Resolver.ResolveUnmanagedDllToPath(unmanagedDllName) is string libraryPath)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return nint.Zero;
    }
}
