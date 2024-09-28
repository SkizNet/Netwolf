using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Netwolf.PluginFramework.Commands;
using Netwolf.PluginFramework.Context;
using Netwolf.PluginFramework.Exceptions;
using Netwolf.PluginFramework.Extensions.DependencyInjection;
using Netwolf.PluginFramework.Permissions;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Test.PluginFramework;

[TestClass]
public class CommandDispatcherTests
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
        await Assert.ThrowsExceptionAsync<NoMatchingPermissionManagerException>(() => dispatcher.DispatchAsync(testB, context, default));
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

        await Assert.ThrowsExceptionAsync<TestException>(() => dispatcher.DispatchAsync(testD, context, default));
        cts.CancelAfter(50);
        await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => dispatcher.DispatchAsync(testE, context, cts.Token));
    }

    private static IServiceScope CreateScope(Type? validatorType = null, IEnumerable<Type>? permissionTypes = null, IEnumerable<Type>? augmenterTypes = null)
    {
        var collection = new ServiceCollection()
            .AddLogging(config => config.SetMinimumLevel(LogLevel.Debug).AddConsole())
            .AddPluginFrameworkServices();

        if (validatorType != null)
        {
            collection.AddSingleton(typeof(ICommandValidator<>), validatorType);
        }

        foreach (var type in permissionTypes ?? [])
        {
            collection.AddSingleton(typeof(IPermissionManager), type);
        }

        foreach (var type in augmenterTypes ?? [])
        {
            collection.AddSingleton(typeof(IContextAugmenter), type);
        }

        return collection.BuildServiceProvider().CreateScope();
    }
}
