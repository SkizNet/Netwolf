using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.BotFramework.Services;

/// <summary>
/// Service that, when given an account, retrieves all permissions associated with that account.
/// If multiple permission providers are registered for a bot, all permissions from all providers
/// will be merged together.
/// </summary>
public interface IPermissionProvider
{
    Task<IEnumerable<string>> GetPermissionsAsync(BotCommandContext context);
}
