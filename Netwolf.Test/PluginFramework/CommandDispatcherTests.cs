using Microsoft.Extensions.DependencyInjection;

using Netwolf.PluginFramework;
using Netwolf.PluginFramework.Commands;
using Netwolf.PluginFramework.Exceptions;

namespace Netwolf.Test.PluginFramework;

[TestClass]
public class CommandDispatcherTests : PluginFrameworkTestBase
{
    [TestMethod]
    public void No_commands_registered_by_default()
    {
        using var scope = CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher<int>>();

        Assert.AreEqual(0, dispatcher.Commands.Length);
    }

    [TestMethod]
    public void Register_commands_by_assembly()
    {
        using var scope = CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher<int>>();
        dispatcher.AddCommandsFromAssembly(typeof(Commands).Assembly);

        // TestC is an ICommandDispatcher<short> so it shouldn't match the provided type (since our type param is invariant)
        // TestF is internal instead of public
        // TestG is generic
        // TestH has improper casing in the command name
        CollectionAssert.AreEquivalent(new string[] { "TESTA", "TESTB", "TESTD", "TESTE" }, dispatcher.Commands);
    }

    [TestMethod]
    public void Regsiter_commands_with_custom_validator()
    {
        using var scope = CreateScope(typeof(Validators.AllowOnlyB<>));
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher<int>>();
        dispatcher.AddCommandsFromAssembly(typeof(Commands).Assembly);

        CollectionAssert.AreEquivalent(new string[] { "TESTB" }, dispatcher.Commands);
    }

    [TestMethod]
    public async Task Dispatch_with_default_permissions()
    {
        using var scope = CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher<int>>();
        dispatcher.AddCommandsFromAssembly(typeof(Commands).Assembly);

        var testA = new TestCommand("TESTA");
        var testB = new TestCommand("TESTB");
        var context = new TestContext();

        Assert.AreEqual(1, await dispatcher.DispatchAsync(testA, context, default));
        await Assert.ThrowsExactlyAsync<NoMatchingPermissionManagerException>(() => dispatcher.DispatchAsync(testB, context, default));
    }

    [TestMethod]
    public async Task Dispatch_with_custom_permissions()
    {
        using var scope = CreateScope(permissionTypes: [typeof(Permissions.AllowAll)]);
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher<int>>();
        dispatcher.AddCommandsFromAssembly(typeof(Commands).Assembly);

        var testA = new TestCommand("TESTA");
        var testB = new TestCommand("TESTB");
        var context = new TestContext();

        Assert.AreEqual(1, await dispatcher.DispatchAsync(testA, context, default));
        Assert.AreEqual(2, await dispatcher.DispatchAsync(testB, context, default));
    }

    [TestMethod]
    public async Task Dispatch_propagates_exceptions()
    {
        using var scope = CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher<int>>();
        dispatcher.AddCommandsFromAssembly(typeof(Commands).Assembly);

        var testD = new TestCommand("TESTD");
        var testE = new TestCommand("TESTE");
        var context = new TestContext();
        var cts = new CancellationTokenSource();

        await Assert.ThrowsExactlyAsync<TestException>(() => dispatcher.DispatchAsync(testD, context, default));
        cts.CancelAfter(50);
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => dispatcher.DispatchAsync(testE, context, cts.Token));
    }

    [DataTestMethod]
    [DataRow(PluginResult.Continue, new int[] { 10, 20, 1 }, DisplayName = "PluginResult.Continue")]
    [DataRow(PluginResult.SuppressDefault, new int[] { 10, 20, 0 }, DisplayName = "PluginResult.SuppressDefault")]
    [DataRow(PluginResult.SuppressPlugins, new int[] { 10, 1 }, DisplayName = "PluginResult.SuppressPlugins")]
    [DataRow(PluginResult.SuppressAll, new int[] { 10, 0 }, DisplayName = "PluginResult.SuppressAll")]
    public async Task Dispatch_with_hooks(PluginResult hookResultType, int[] expectedResults)
    {
        using var scope = CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher<int>>();
        var registry = scope.ServiceProvider.GetRequiredService<ICommandHookRegistry>();
        List<int> results = [];

        dispatcher.AddCommandsFromAssembly(typeof(Commands).Assembly);
        using var hook1 = registry.AddCommandHook(new TestHandler<PluginResult>("TESTA", hookResultType, () => results.Add(10)));
        using var hook2 = registry.AddCommandHook(new TestHandler<PluginResult>("TESTA", hookResultType, () => results.Add(20)));

        var testA = new TestCommand("TESTA");
        var context = new TestContext();

        // when suppressing default, the dispatcher returns the default value for TResult, which is 0 for int
        results.Add(await dispatcher.DispatchAsync(testA, context, default));
        CollectionAssert.AreEqual(expectedResults, results);
    }
}
