using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Netwolf.BotFramework;
using Netwolf.BotFramework.Services;
using Netwolf.PluginFramework.Commands;
using Netwolf.Transport.Commands;
using Netwolf.Transport.IRC;

using System.ComponentModel.DataAnnotations;

namespace Netwolf.Test.BotFramework;

// These tests do not attempt to test Bot.ParseCommandAndArgs, so generated ICommands in the context must be correctly defined
[TestClass]
public class GeneratorTests
{
    private const string BOT_NAME = "test";

    private ServiceProvider Container { get; init; }

    public GeneratorTests()
    {
        var services = new ServiceCollection();
        services.AddLogging(config => config.SetMinimumLevel(LogLevel.Debug).AddConsole());
        services.AddBot<TestBot>(BOT_NAME);
        services.AddSingleton<IOptionsMonitor<BotOptions>>(new TestOptionsMonitor<BotOptions>() { CurrentValue = new BotOptions() });
        Container = services.BuildServiceProvider();
    }

    private Task<BotCommandResult?> RunTest(string commandName, string param = "")
    {
        using var scope = Container.CreateScope();
        var data = scope.ServiceProvider.GetRequiredKeyedService<BotCreationData>(BOT_NAME);

        // we want to ensure source generated commands are used for this test
        data.ForceCommandOptimization = true;
        var bot = new TestBot(data);
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher<BotCommandResult>>();
        var commandFactory = scope.ServiceProvider.GetRequiredService<ICommandFactory>();
        var validationContextFactory = scope.ServiceProvider.GetRequiredService<IValidationContextFactory>();
        var command = commandFactory.CreateCommand(CommandType.Bot, "test", commandName.ToUpperInvariant(), param == string.Empty ? [] : [param], new Dictionary<string, string?>(), null);
        var fullLine = (param == string.Empty) ? $"!{commandName}" : $"!{commandName} {param}";
        var context = new BotCommandContext(bot, validationContextFactory, BOT_NAME, command, fullLine);

        return dispatcher.DispatchAsync(command, context, default);
    }

    [TestMethod]
    public void Successfully_find_generated_commands()
    {
        List<string> expected = ["SYNCVOID0", "SYNCINT0", "ASYNCTASK0", "ASYNCTASKINT0", "SYNCINT1", "COMPLEX"];

        using var scope = Container.CreateScope();
        var data = scope.ServiceProvider.GetRequiredKeyedService<BotCreationData>(BOT_NAME);

        // we want to ensure source generated commands are used for this test
        data.ForceCommandOptimization = true;
        var bot = new TestBot(data);
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher<BotCommandResult>>();

        CollectionAssert.AreEquivalent(expected, dispatcher.Commands);
    }

    [TestMethod]
    public async Task Successfully_call_generated_sync_void_0()
    {
        var result = await RunTest("syncvoid0");
        Assert.IsNotNull(result);
        Assert.IsNull(result.Value);
    }

    [TestMethod]
    public async Task Successfully_call_generated_sync_int_0()
    {
        var result = await RunTest("syncint0");
        Assert.IsNotNull(result);
        Assert.AreEqual(42, result.Value);
    }

    [DataTestMethod]
    [DataRow("123", 123, DisplayName = "positive int")]
    [DataRow("-42", -42, DisplayName = "negative int")]
    [DataRow("", 0, DisplayName = "missing param")]
    [DataRow("123 456", 123, DisplayName = "extra param")]
    public async Task Successfully_call_generated_sync_int_1(string param, int expected)
    {
        var result = await RunTest("syncint1", param);
        Assert.IsNotNull(result);
        Assert.AreEqual(expected, result.Value);
    }

    [TestMethod]
    public async Task Successfully_call_generated_async_task_0()
    {
        var result = await RunTest("asynctask0");
        Assert.IsNotNull(result);
        Assert.IsNull(result.Value);
    }

    [TestMethod]
    public async Task Successfully_call_generated_async_task_int_0()
    {
        var result = await RunTest("asynctaskint0");
        Assert.IsNotNull(result);
        Assert.AreEqual(42, result.Value);
    }

    [DataTestMethod]
    [DataRow("CommandName 0", "COMPLEX")]
    [DataRow("SenderNick 0", "test")]
    [DataRow("FullLine 0 0.1 0.2  foo   bar baz  ", "!complex FullLine 0 0.1 0.2  foo   bar baz  ")]
    [DataRow("RawArgs 0 0.1 0.2  foo   bar baz  ", "RawArgs 0 0.1 0.2  foo   bar baz  ")]
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
    public async Task Successfully_call_generated_complex(string param, string expected)
    {
        var result = await RunTest("complex", param);
        Assert.IsNotNull(result);
        Assert.AreEqual(expected, result.Value);
    }

    [DataTestMethod]
    [DataRow("CommandName", DisplayName = "No param")]
    [DataRow("CommandName qq", DisplayName = "Invalid param")]
    [DataRow("CommandName -9999999999999", DisplayName = "Below int.MinValue")]
    [DataRow("CommandName 9999999999999", DisplayName = "Above int.MaxValue")]
    public async Task Fail_missing_required_param(string param)
    {
        // capture the Task so we can also examine properties on the exception itself to verify the *correct* exception was thrown
        var task = RunTest("complex", param);
        await Assert.ThrowsExceptionAsync<ValidationException>(() => task);
        try
        {
            await task;
        }
        catch (ValidationException ex)
        {
            Assert.IsInstanceOfType<RequiredAttribute>(ex.ValidationAttribute);
        }
    }

    [DataTestMethod]
    [DataRow("CommandName -1", DisplayName = "Too low")]
    [DataRow("CommandName 582", DisplayName = "Too high")]
    public async Task Fail_invalid_range_param(string param)
    {
        // capture the Task so we can also examine properties on the exception itself to verify the *correct* exception was thrown
        var task = RunTest("complex", param);
        await Assert.ThrowsExceptionAsync<ValidationException>(() => task);
        try
        {
            await task;
        }
        catch (ValidationException ex)
        {
            Assert.IsInstanceOfType<RangeAttribute>(ex.ValidationAttribute);
        }
    }
}
