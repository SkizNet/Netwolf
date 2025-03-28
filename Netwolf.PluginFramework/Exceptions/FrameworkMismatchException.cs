// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Reflection;

namespace Netwolf.PluginFramework.Exceptions;

/// <summary>
/// An exception thrown when a plugin cannot be loaded due to a Netwolf dependency
/// not meeting version constraints (mistmatched major version, or a too-new minor version).
/// </summary>
public class FrameworkMismatchException : FrameworkLoadException
{
    /// <summary>
    /// The Netwolf assembly the plugin attempted to load
    /// </summary>
    public AssemblyName ExistingAssembly { get; init; }

    /// <summary>
    /// Construct a new FrameworkMismatchException
    /// </summary>
    /// <param name="pluginName">The name of the faulted plugin</param>
    /// <param name="loadingAssembly">The Netwolf assembly the plugin attempted to load</param>
    /// <param name="existingAssembly">The Netwolf assembly that was already loaded by the base</param>
    public FrameworkMismatchException(string pluginName, AssemblyName loadingAssembly, AssemblyName existingAssembly)
        : base(pluginName, loadingAssembly, "Incompatible plugin: The plugin depends on an unsupported Netwolf framework version")
    {
        ExistingAssembly = existingAssembly;
    }
}
