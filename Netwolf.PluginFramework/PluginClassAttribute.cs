// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.PluginFramework;

/// <summary>
/// Attribute used to declare that this assembly is a Netwolf plugin,
/// with a reference to the type implementing IPlugin used to obtain
/// metadata and perform initialization.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public class PluginClassAttribute : Attribute
{
    /// <summary>
    /// Type of the main plugin class that implements IPlugin
    /// </summary>
    public Type PluginType { get; init; }

    /// <summary>
    /// Declares that this assembly is a Netwolf plugin
    /// </summary>
    /// <param name="pluginType">Class which implements IPlugin</param>
    public PluginClassAttribute(Type pluginType)
    {
        PluginType = pluginType;
    }
}
