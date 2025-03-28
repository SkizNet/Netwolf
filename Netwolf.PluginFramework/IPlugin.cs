// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.PluginFramework;

/// <summary>
/// Interface that all loadable plugins must implement.
/// </summary>
public interface IPlugin
{
    /// <summary>
    /// Plugin name
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Plugin description
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Plugin author
    /// </summary>
    string Author { get; }

    /// <summary>
    /// Plugin version
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Initialize the plugin; this method is called once upon plugin load.
    /// </summary>
    /// <param name="host"></param>
    void Initialize(IPluginHost host);
}
