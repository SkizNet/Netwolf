using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Netwolf.BotFramework;
using Netwolf.BotFramework.Services;
using Netwolf.PluginFramework.Commands;
using Netwolf.Transport.IRC;
using Netwolf.Transport.IRC.Fakes;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Test.BotFramework;

[TestClass]
public class ThunkTests
{
    private const string BOT_NAME = "test";

    private ServiceProvider Container { get; init; }

    public ThunkTests()
    {
        Container = new ServiceCollection()
            .AddLogging(config => config.AddConsole())
            .AddBot<TestBot>(BOT_NAME)
            .BuildServiceProvider();
    }

    [TestMethod]
    public void Successfully_make_thunks()
    {
        List<string> expected = ["SYNCVOID0", "SYNCINT0", "ASYNCTASK0", "ASYNCTASKINT0", "SYNCINT1"];

        using var scope = Container.CreateScope();
        var data = scope.ServiceProvider.GetRequiredKeyedService<BotCreationData>(BOT_NAME);

        // we want to generate thunks for this test
        data.EnableCommandOptimization = false;
        var bot = new TestBot(data);
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher<BotCommandResult>>();

        CollectionAssert.AreEquivalent(expected, dispatcher.Commands);
    }

    [TestMethod]
    public async Task Successfully_call_thunk_sync_void_0()
    {
        using var scope = Container.CreateScope();
        var data = scope.ServiceProvider.GetRequiredKeyedService<BotCreationData>(BOT_NAME);

        // we want to generate thunks for this test
        data.EnableCommandOptimization = false;
        var bot = new TestBot(data);
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher<BotCommandResult>>();
        var commandFactory = scope.ServiceProvider.GetRequiredService<ICommandFactory>();
        var command = commandFactory.CreateCommand(CommandType.Bot, "test", "SYNCVOID0", [], new Dictionary<string, string?>(), null);
        var context = new BotCommandContext(bot, command, string.Empty);

        var result = await dispatcher.DispatchAsync(command, context, default);
        Assert.IsNotNull(result);
        Assert.IsNull(result.Value);
    }

    [TestMethod]
    public async Task Successfully_call_thunk_sync_int_0()
    {
        using var scope = Container.CreateScope();
        var data = scope.ServiceProvider.GetRequiredKeyedService<BotCreationData>(BOT_NAME);

        // we want to generate thunks for this test
        data.EnableCommandOptimization = false;
        var bot = new TestBot(data);
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher<BotCommandResult>>();
        var commandFactory = scope.ServiceProvider.GetRequiredService<ICommandFactory>();
        var command = commandFactory.CreateCommand(CommandType.Bot, "test", "SYNCINT0", [], new Dictionary<string, string?>(), null);
        var context = new BotCommandContext(bot, command, string.Empty);

        var result = await dispatcher.DispatchAsync(command, context, default);
        Assert.IsNotNull(result);
        Assert.AreEqual(42, result.Value);
    }

    [TestMethod]
    public async Task Successfully_call_thunk_sync_int_1()
    {
        using var scope = Container.CreateScope();
        var data = scope.ServiceProvider.GetRequiredKeyedService<BotCreationData>(BOT_NAME);

        // we want to generate thunks for this test
        data.EnableCommandOptimization = false;
        var bot = new TestBot(data);
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher<BotCommandResult>>();
        var commandFactory = scope.ServiceProvider.GetRequiredService<ICommandFactory>();
        var command = commandFactory.CreateCommand(CommandType.Bot, "test", "SYNCINT1", [], new Dictionary<string, string?>(), null);
        var context = new BotCommandContext(bot, command, "123");

        var result = await dispatcher.DispatchAsync(command, context, default);
        Assert.IsNotNull(result);
        Assert.AreEqual(123, result.Value);
    }

    [TestMethod]
    public async Task Successfully_call_thunk_async_task_0()
    {
        using var scope = Container.CreateScope();
        var data = scope.ServiceProvider.GetRequiredKeyedService<BotCreationData>(BOT_NAME);

        // we want to generate thunks for this test
        data.EnableCommandOptimization = false;
        var bot = new TestBot(data);
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher<BotCommandResult>>();
        var commandFactory = scope.ServiceProvider.GetRequiredService<ICommandFactory>();
        var command = commandFactory.CreateCommand(CommandType.Bot, "test", "ASYNCTASK0", [], new Dictionary<string, string?>(), null);
        var context = new BotCommandContext(bot, command, string.Empty);

        var result = await dispatcher.DispatchAsync(command, context, default);
        Assert.IsNotNull(result);
        Assert.IsNull(result.Value);
    }

    [TestMethod]
    public async Task Successfully_call_thunk_async_task_int_0()
    {
        using var scope = Container.CreateScope();
        var data = scope.ServiceProvider.GetRequiredKeyedService<BotCreationData>(BOT_NAME);

        // we want to generate thunks for this test
        data.EnableCommandOptimization = false;
        var bot = new TestBot(data);
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher<BotCommandResult>>();
        var commandFactory = scope.ServiceProvider.GetRequiredService<ICommandFactory>();
        var command = commandFactory.CreateCommand(CommandType.Bot, "test", "ASYNCTASKINT0", [], new Dictionary<string, string?>(), null);
        var context = new BotCommandContext(bot, command, string.Empty);

        var result = await dispatcher.DispatchAsync(command, context, default);
        Assert.IsNotNull(result);
        Assert.AreEqual(42, result.Value);
    }
}
