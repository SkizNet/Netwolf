using Netwolf.PluginFramework.Commands;
using Netwolf.PluginFramework.Context;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.BotFramework;

public class BotCommandContext : IContext
{
    public Bot Bot { get; init; }

    public ICommand Command { get; init; }

    public string FullLine { get; init; }

    public string SenderNickname { get; init; }

    /// <summary>
    /// Recognized account of the sender
    /// </summary>
    public string? SenderAccount { get; set; }

    /// <summary>
    /// Type used to determine the <see cref="SenderAccount"/> property
    /// </summary>
    public Type? AccountProvider { get; set; }

    /// <summary>
    /// Permissions belonging to the <see cref="SenderAccount"/>
    /// </summary>
    public HashSet<string> SenderPermissions { get; init; } = [];

    /// <summary>
    /// Types used to populate <see cref="SenderPermissions"/>;
    /// this will only contain types that actually added new elements to the permissions set.
    /// </summary>
    public List<Type> PermissionProviders { get; init; } = [];

    public BotCommandContext(Bot bot, ICommand command, string fullLine)
    {
        Bot = bot;
        Command = command;
        FullLine = fullLine;

        // TODO: split out just the nickname if this is a full nick!user@host hostmask
        SenderNickname = command.Source!;
    }
}
