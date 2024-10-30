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

    private UserRecordLookup UserRecordLookup { get; init; }

    public ServicesAccountProvider(UserRecordLookup userRecordLookup)
    {
        UserRecordLookup = userRecordLookup;
    }

    public Task<string?> GetAccountAsync(BotCommandContext context, CancellationToken cancellationToken)
    {
        var user = UserRecordLookup.GetUserByNick(context.SenderNickname);
        return Task.FromResult(context.Command.Tags.GetValueOrDefault(TAG_NAME) ?? user?.Account);
    }

    public bool ShouldEnable(string cap, string? value)
    {
        return cap == CAP_NAME;
    }
}
