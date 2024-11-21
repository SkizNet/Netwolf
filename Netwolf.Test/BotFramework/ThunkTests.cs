using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Netwolf.BotFramework;
using Netwolf.BotFramework.Services;
using Netwolf.PluginFramework.Commands;
using Netwolf.Transport.IRC;

namespace Netwolf.Test.BotFramework;

// These tests do not attempt to test Bot.ParseCommandAndArgs, so generated ICommands in the context must be correctly defined
[TestClass]
public class ThunkTests
{
    private const string BOT_NAME = "test";

    private ServiceProvider Container { get; init; }

    public ThunkTests()
    {
        var services = new ServiceCollection();
        services.AddLogging(config => config.SetMinimumLevel(LogLevel.Debug).AddConsole());
        services.AddBot<TestBot>(BOT_NAME);
        services.AddSingleton<IOptionsMonitor<BotOptions>>(new TestOptionsMonitor<BotOptions>() { CurrentValue = new BotOptions() });
        Container = services.BuildServiceProvider();
    }

    [TestMethod]
    public void Successfully_make_thunks()
    {
        List<string> expected = ["SYNCVOID0", "SYNCINT0", "ASYNCTASK0", "ASYNCTASKINT0", "SYNCINT1", "COMPLEX"];

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
        var context = new BotCommandContext(bot, BOT_NAME, command, "!syncvoid0", string.Empty);

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
        var context = new BotCommandContext(bot, BOT_NAME, command, "!syncint0", string.Empty);

        var result = await dispatcher.DispatchAsync(command, context, default);
        Assert.IsNotNull(result);
        Assert.AreEqual(42, result.Value);
    }

    [DataTestMethod]
    [DataRow("123", 123, DisplayName = "positive int")]
    [DataRow("-42", -42, DisplayName = "negative int")]
    [DataRow("", 0, DisplayName = "missing param")]
    [DataRow("123 456", 123, DisplayName = "extra param")]
    public async Task Successfully_call_thunk_sync_int_1(string param, int expected)
    {
        using var scope = Container.CreateScope();
        var data = scope.ServiceProvider.GetRequiredKeyedService<BotCreationData>(BOT_NAME);

        // we want to generate thunks for this test
        data.EnableCommandOptimization = false;
        var bot = new TestBot(data);
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher<BotCommandResult>>();
        var commandFactory = scope.ServiceProvider.GetRequiredService<ICommandFactory>();
        var command = commandFactory.CreateCommand(CommandType.Bot, "test", "SYNCINT1", param.Split(' ', StringSplitOptions.RemoveEmptyEntries), new Dictionary<string, string?>(), null);
        var context = new BotCommandContext(bot, BOT_NAME, command, $"!syncint1 {param}", param);

        var result = await dispatcher.DispatchAsync(command, context, default);
        Assert.IsNotNull(result);
        Assert.AreEqual(expected, result.Value);
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
        var context = new BotCommandContext(bot, BOT_NAME, command, "!asynctask0", string.Empty);

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
        var context = new BotCommandContext(bot, BOT_NAME, command, "!asynctaskint0", string.Empty);

        var result = await dispatcher.DispatchAsync(command, context, default);
        Assert.IsNotNull(result);
        Assert.AreEqual(42, result.Value);
    }

    [DataTestMethod]
    [DataRow("CommandName 0", "COMPLEX")]
    [DataRow("SenderNick 0", "test")]
    [DataRow("FullLine 0 0.1 0.2  foo   bar baz  ", "!complex FullLine 0 0.1 0.2  foo   bar baz  ")]
    [DataRow("RawArgs 0 0.1 0.2  foo   bar baz  ", "RawArgs 0 0.1 0.2  foo   bar baz  ")]
    [DataRow("NumArgs 0 0.1 0.2  foo   bar baz  ", "7")]
    [DataRow("IntVal 99", "99")]
    [DataRow("NumDoubles 99", "0")]
    [DataRow("NumDoubles 99 1", "1")]
    [DataRow("NumDoubles 99 1.2 2.3", "2")]
    [DataRow("NumDoubles 99 1.2 qq 2.3", "1")]
    [DataRow("FirstDouble 99 1.2 2.3 3.4", "1.2")]
    [DataRow("LastDouble 99 1.2 2.3 3.4", "3.4")]
    [DataRow("Rest 99", "")]
    [DataRow("Rest 99 1.2 2.3", "")]
    [DataRow("Rest 99 1.2 qq 2.3", "qq 2.3")]
    [DataRow("Rest 0 0.1 0.2  foo   bar baz  ", "foo   bar baz  ")]
    public async Task Successfully_call_thunk_complex(string param, string expected)
    {
        using var scope = Container.CreateScope();
        var data = scope.ServiceProvider.GetRequiredKeyedService<BotCreationData>(BOT_NAME);

        // we want to generate thunks for this test
        data.EnableCommandOptimization = false;
        var bot = new TestBot(data);
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher<BotCommandResult>>();
        var commandFactory = scope.ServiceProvider.GetRequiredService<ICommandFactory>();
        var command = commandFactory.CreateCommand(CommandType.Bot, "test", "COMPLEX", param.Split(' ', StringSplitOptions.RemoveEmptyEntries), new Dictionary<string, string?>(), null);
        var context = new BotCommandContext(bot, BOT_NAME, command, $"!complex {param}", param);

        var result = await dispatcher.DispatchAsync(command, context, default);
        Assert.IsNotNull(result);
        Assert.AreEqual(expected, result.Value);
    }
}
