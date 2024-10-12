using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.BotFramework;

/// <summary>
/// Result of a bot command. This is a no-op class right now
/// only present so that multiple frameworks can be added in a single assembly.
/// Bot commands should simply return BotCommandResult.CompletedCommand in their handlers.
/// </summary>
public record BotCommandResult(object? Value = null)
{
    public static BotCommandResult CompletedCommand { get; private set; } = new();
}
