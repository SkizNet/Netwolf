using Microsoft.Extensions.DependencyInjection;

using Netwolf.PluginFramework.Loader;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Test.PluginFramework;

[TestClass]
public class PluginLoaderTests : PluginFrameworkTestBase
{
    [TestMethod]
    public void Successfully_load_plugin()
    {
        using var provider = CreateProvider();
        var loader = provider.GetRequiredService<IPluginLoader>();
        var currentPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var status = loader.Load($"{currentPath}/TestPlugin1.dll", out var plugin);
        
        Assert.AreEqual(PluginLoadStatus.Success, status, "Plugin should load successfully");
        Assert.IsNotNull(plugin, "Plugin should not be null");
        Assert.AreEqual(1, loader.LoadedPlugins.Count(), "One plugin should be loaded");
        Assert.AreEqual("Test1", plugin.Name, "Plugin name should match");
        Assert.AreEqual("Unit testing plugin", plugin.Description, "Plugin description should match");
        Assert.AreEqual("Netwolf contributors", plugin.Author, "Plugin author should match");
        Assert.AreEqual("1.0.0", plugin.Version, "Plugin version should match");
    }

    [TestMethod]
    public void Successfully_unload_plugin()
    {
        using var provider = CreateProvider();
        var loader = provider.GetRequiredService<IPluginLoader>();
        var currentPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        var status = loader.Load($"{currentPath}/TestPlugin1.dll", out var plugin);
        Assert.AreEqual(PluginLoadStatus.Success, status, "Plugin should load successfully");
        Assert.IsNotNull(plugin, "Plugin should not be null");
        Assert.AreEqual(1, loader.LoadedPlugins.Count(), "One plugin should be loaded");

        status = loader.Unload(plugin.Id);
        Assert.AreEqual(PluginLoadStatus.Success, status, "Plugin should unload successfully");
        Assert.AreEqual(0, loader.LoadedPlugins.Count(), "No plugins should be loaded after unload");
    }

    [TestMethod]
    public void Successfully_load_plugin_twice_returns_same_plugin()
    {
        using var provider = CreateProvider();
        var loader = provider.GetRequiredService<IPluginLoader>();
        var currentPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var status = loader.Load($"{currentPath}/TestPlugin1.dll", out var plugin1);
        Assert.AreEqual(PluginLoadStatus.Success, status, "Plugin should load successfully");

        status = loader.Load($"{currentPath}/TestPlugin1.dll", out var plugin2);
        Assert.AreEqual(PluginLoadStatus.AlreadyLoaded, status, "Plugin should be marked as already loaded");
        Assert.AreEqual(plugin1, plugin2, "Second load should return the same metadata");
    }

    [TestMethod]
    public void Successfully_collect_ALC()
    {
        using var provider = CreateProvider();
        var loader = (PluginLoader)provider.GetRequiredService<IPluginLoader>();
        var currentPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        
        var status = loader.Load($"{currentPath}/TestPlugin1.dll", out var plugin);
        Assert.AreEqual(PluginLoadStatus.Success, status, "Plugin should load successfully");
        Assert.IsNotNull(plugin, "Plugin should not be null");

        status = loader.Unload(plugin.Id);
        Assert.AreEqual(PluginLoadStatus.Success, status, "Plugin should unload successfully");

        // now try to collect the ALC
        for (int i = 0; i < 10 && plugin.IsLoaded; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Assert.IsFalse(plugin.IsLoaded, "Plugin's AssemblyLoadContext should have unloaded");
    }

    [TestMethod]
    public void Fail_loading_plugin_without_IPlugin()
    {
        using var provider = CreateProvider();
        var loader = provider.GetRequiredService<IPluginLoader>();
        var currentPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var status = loader.Load($"{currentPath}/TestPlugin2.dll", out var plugin);

        Assert.AreEqual(PluginLoadStatus.NotAPlugin, status, "Plugin should fail to load due to missing IPlugin interface");
        Assert.IsNull(plugin, "Plugin metadata should be null");
        Assert.AreEqual(0, loader.LoadedPlugins.Count(), "No plugins should be loaded");
    }

    [TestMethod]
    public void Fail_loading_plugin_without_PluginClassAttribute()
    {
        using var provider = CreateProvider();
        var loader = provider.GetRequiredService<IPluginLoader>();
        var currentPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var status = loader.Load($"{currentPath}/TestPlugin3.dll", out var plugin);

        Assert.AreEqual(PluginLoadStatus.NotAPlugin, status, "Plugin should fail to load due to missing PluginClassAttribute");
        Assert.IsNull(plugin, "Plugin metadata should be null");
        Assert.AreEqual(0, loader.LoadedPlugins.Count(), "No plugins should be loaded");
    }

    [TestMethod]
    public void Fail_loading_plugin_with_Initialize_exception()
    {
        using var provider = CreateProvider();
        var loader = provider.GetRequiredService<IPluginLoader>();
        var currentPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var status = loader.Load($"{currentPath}/TestPlugin4.dll", out var plugin);

        Assert.AreEqual(PluginLoadStatus.UnknownError, status, "Plugin should fail to load due to exception thrown during Initialize");
        Assert.IsNull(plugin, "Plugin metadata should be null");
        Assert.AreEqual(0, loader.LoadedPlugins.Count(), "No plugins should be loaded");
    }
}
