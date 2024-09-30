using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.BotFramework.Services;

/// <summary>
/// Service to resolve a sender into an account recognized by the bot.
/// If multiple account providers are registered to a single bot, they are tried in order;
/// the first one that returns a valid account will be used.
/// </summary>
public interface IAccountProvider
{
    Task<string?> GetAccountAsync(BotCommandContext context);
}
