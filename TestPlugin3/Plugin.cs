// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: GPL-3.0-or-later

using Netwolf.PluginFramework;

// Invalid: Plugin lacks the PluginClass attribute on the assembly

namespace TestPlugin3;

public class Plugin : IPlugin
{
    public string Name => "Test3";
    public string Description => "Unit testing plugin";
    public string Author => "Netwolf contributors";
    public string Version => "1.0.0";

    public void Initialize(IPluginHost host)
    {
        // No initialization here; this plugin does nothing
    }
}
