// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: GPL-3.0-or-later

using Netwolf.PluginFramework;

[assembly: PluginClass(typeof(TestPlugin2.Plugin))]

namespace TestPlugin2;

// Invalid: TestPlugin2 does not implement IPlugin
public class Plugin
{
    public string Name => "Test2";
    public string Description => "Unit testing plugin";
    public string Author => "Netwolf contributors";
    public string Version => "1.0.0";

    public void Initialize(IPluginHost host)
    {
        // No initialization here; this plugin does nothing
    }
}
