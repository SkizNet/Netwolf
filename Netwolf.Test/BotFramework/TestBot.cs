using Microsoft.Extensions.Logging;

using Netwolf.Attributes;
using Netwolf.BotFramework;
using Netwolf.BotFramework.Services;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Test.BotFramework;

internal class TestBot : Bot
{
    public TestBot(BotCreationData data)
        : base(data) { }


    [Command("SYNCVOID0")]
    public void SyncVoid0()
    {
        Logger.LogInformation("SyncVoid0 Called");
    }

    [Command("SYNCINT0")]
    public int SyncInt0()
    {
        Logger.LogInformation("SyncInt0 Called");
        return 42;
    }

    [Command("ASYNCTASK0")]
    public Task AsyncTask0()
    {
        Logger.LogInformation("AsyncTask0 Called");
        return Task.CompletedTask;
    }

    [Command("ASYNCTASKINT0")]
    public Task<int> AsyncTaskInt0()
    {
        Logger.LogInformation("AsyncTaskInt0 Called");
        return Task.FromResult(42);
    }

    [Command("SYNCINT1")]
    public int SyncInt1(int param)
    {
        Logger.LogInformation("SyncInt1 Called");
        return param;
    }

    [Command("COMPLEX")]
    public string Complex(
        BotCommandContext context,
        [CommandName]
        string commandName,
        CommandTestType testType,
        [Required, Range(0, 100)]
        int requiredInt,
        double[] extraNumbers,
        [Rest]
        string rest)
    {
        return testType switch
        {
            CommandTestType.CommandName => commandName,
            CommandTestType.SenderNick => context.SenderNickname,
            CommandTestType.FullLine => context.FullLine,
            CommandTestType.NumArgs => context.Command.Args.Count.ToString(),
            CommandTestType.RawArgs => context.RawArgs,
            CommandTestType.IntVal => requiredInt.ToString(),
            CommandTestType.NumDoubles => extraNumbers.Length.ToString(),
            CommandTestType.FirstDouble => extraNumbers.FirstOrDefault().ToString(),
            CommandTestType.LastDouble => extraNumbers.LastOrDefault().ToString(),
            CommandTestType.Rest => rest,
            _ => throw new NotImplementedException()
        };
    }
}
