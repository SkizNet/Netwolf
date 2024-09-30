using Netwolf.BotFramework.Services;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.BotFramework.Accounts;

internal class ServicesAccountProvider : IAccountProvider, ICapProvider
{
    private const string CAP_NAME = "account-tag";
    private const string TAG_NAME = "account";

    public Task<string?> GetAccountAsync(BotCommandContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult(context.Command.Tags.GetValueOrDefault(TAG_NAME));
    }

    public bool ShouldEnable(string cap, string? value)
    {
        return cap == CAP_NAME;
    }
}
