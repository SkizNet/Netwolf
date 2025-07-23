// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: GPL-3.0-or-later

using Netwolf.PluginFramework;

[assembly: PluginClass(typeof(TestPlugin4.Plugin))]

namespace TestPlugin4;

public class Plugin : IPlugin
{
    public string Name => "Test4";
    public string Description => "Unit testing plugin";
    public string Author => "Netwolf contributors";
    public string Version => "1.0.0";

    public void Initialize(IPluginHost host)
    {
        // Invalid: Initialize throws an exception
        throw new NotImplementedException();
    }
}
