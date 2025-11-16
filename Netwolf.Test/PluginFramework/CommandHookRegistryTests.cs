using Microsoft.Extensions.DependencyInjection;

using Netwolf.PluginFramework;
using Netwolf.PluginFramework.Commands;

namespace Netwolf.Test.PluginFramework;

[TestClass]
public class CommandHookRegistryTests : PluginFrameworkTestBase
{
    [TestMethod]
    public void Hooks_register_correctly()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<ICommandHookRegistry>();

        using var hook1 = registry.AddCommandHook(new TestHandler<PluginResult>("HOOK1", PluginResult.Continue));
        using var hook2 = registry.AddCommandHook(new TestHandler<PluginResult>("HOOK2", PluginResult.Continue));
        CollectionAssert.AreEquivalent(new string[] { "HOOK1", "HOOK2" }, registry.GetHookedCommandNames().ToList());
    }

    [TestMethod]
    public void Hooks_unregister_correctly()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<ICommandHookRegistry>();

        using var hook1 = registry.AddCommandHook(new TestHandler<PluginResult>("HOOK1", PluginResult.Continue));
        using (var hook2 = registry.AddCommandHook(new TestHandler<PluginResult>("HOOK2", PluginResult.Continue)))
        {
            CollectionAssert.AreEquivalent(new string[] { "HOOK1", "HOOK2" }, registry.GetHookedCommandNames().ToList());
        }

        CollectionAssert.AreEquivalent(new string[] { "HOOK1" }, registry.GetHookedCommandNames().ToList());
    }

    [TestMethod]
    public void Hooks_create_dispatcher_commands()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<ICommandHookRegistry>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher<int>>();

        using var hook1 = registry.AddCommandHook(new TestHandler<PluginResult>("HOOK1", PluginResult.Continue));
        using var hook2 = registry.AddCommandHook(new TestHandler<PluginResult>("HOOK2", PluginResult.Continue));
        CollectionAssert.AreEquivalent(new string[] { "HOOK1", "HOOK2" }, dispatcher.Commands);
    }

    [TestMethod]
    [DataRow("INVALID", 0)]
    [DataRow("HOOK1", 1)]
    [DataRow("HOOK2", 2)]
    public void Retrieve_hooks(string command, int count)
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<ICommandHookRegistry>();

        using var hook1 = registry.AddCommandHook(new TestHandler<PluginResult>("HOOK1", PluginResult.Continue));
        using var hook2_1 = registry.AddCommandHook(new TestHandler<PluginResult>("HOOK2", PluginResult.Continue));
        using var hook2_2 = registry.AddCommandHook(new TestHandler<PluginResult>("HOOK2", PluginResult.SuppressDefault));

        var definedHooks = registry.GetCommandHooks(command);
        Assert.AreEqual(count, definedHooks.Count());
    }

    [TestMethod]
    public async Task Hooks_in_priority_order()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<ICommandHookRegistry>();
        List<int> order = [];

        using var hook1 = registry.AddCommandHook(new TestHandler<PluginResult>("HOOK1", PluginResult.Continue, () => order.Add(1)), CommandHookPriority.Normal);
        using var hook2 = registry.AddCommandHook(new TestHandler<PluginResult>("HOOK1", PluginResult.Continue, () => order.Add(2)), CommandHookPriority.Highest);
        using var hook3 = registry.AddCommandHook(new TestHandler<PluginResult>("HOOK1", PluginResult.Continue, () => order.Add(3)), CommandHookPriority.Lowest);
        using var hook4 = registry.AddCommandHook(new TestHandler<PluginResult>("HOOK1", PluginResult.Continue, () => order.Add(4)), CommandHookPriority.Normal);
        using var hook5 = registry.AddCommandHook(new TestHandler<PluginResult>("HOOK1", PluginResult.Continue, () => order.Add(5)), CommandHookPriority.Low);
        using var hook6 = registry.AddCommandHook(new TestHandler<PluginResult>("HOOK1", PluginResult.Continue, () => order.Add(6)), CommandHookPriority.High);

        foreach (var hook in registry.GetCommandHooks("HOOK1"))
        {
            await hook.ExecuteAsync(null!, null!, default);
        }

        CollectionAssert.AreEqual(new int[] { 2, 6, 1, 4, 5, 3 }, order);
    }
}
