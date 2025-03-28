// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Reflection;

namespace Netwolf.PluginFramework.Exceptions;

/// <summary>
/// Base class for exceptions generated when attempting to load
/// Netwolf framework dependencies in plugins
/// </summary>
public class FrameworkLoadException : InvalidOperationException
{
    /// <summary>
    /// The name of the faulted plugin
    /// </summary>
    public string PluginName { get; init; }

    /// <summary>
    /// The Netwolf assembly the plugin attempted to load
    /// </summary>
    public AssemblyName LoadingAssembly { get; init; }

    public FrameworkLoadException(string pluginName, AssemblyName loadingAssembly, string reason)
        : base(reason)
    {
        PluginName = pluginName;
        LoadingAssembly = loadingAssembly;
    }
}
