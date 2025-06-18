// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: GPL-3.0-or-later

using Netwolf.PluginFramework;

// The PluginClassAttribute is used to let the plugin loader know which class to instantiate for this plugin.
// Plugins are **required** to have this attribute.
[assembly: PluginClass(typeof(Netwolf.ExamplePlugin.Plugin))]

namespace Netwolf.ExamplePlugin;

// The Plugin class **must** implement IPlugin
public class Plugin : IPlugin
{
    public string Name => "Example";
    public string Description => "Example plugin demonstrating a basic skeleton for writing plugins and for unit testing";
    public string Author => "Netwolf contributors";
    public string Version => "1.0.0";

    public void Initialize(IPluginHost host)
    {
        throw new NotImplementedException();
    }
}
