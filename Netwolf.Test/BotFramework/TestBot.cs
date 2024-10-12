using Microsoft.Extensions.Logging;

using Netwolf.Attributes;
using Netwolf.BotFramework;

using System;
using System.Collections.Generic;
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
}
