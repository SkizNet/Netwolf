// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Reflection;

namespace Netwolf.PluginFramework.Exceptions;

/// <summary>
/// An exception thrown when a plugin cannot be loaded due to a Netwolf dependency
/// not being already loaded by the default AssemblyLoadContext.
/// </summary>
public class UnloadedFrameworkException : FrameworkLoadException
{
    /// <summary>
    /// Construct a new UnloadedFrameworkException
    /// </summary>
    /// <param name="pluginName">The name of the faulted plugin</param>
    /// <param name="loadingAssembly">The Netwolf assembly the plugin attempted to load</param>
    public UnloadedFrameworkException(string pluginName, AssemblyName loadingAssembly)
        : base(pluginName, loadingAssembly, "Incompatible plugin: The plugin depends on a Netwolf framework not loaded by the base assembly")
    {
        
    }
}
