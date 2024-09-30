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
    /// Recognized account of the sender; populated via context augmenter
    /// </summary>
    public string? SenderAccount { get; set; }

    /// <summary>
    /// Augmenter used to populate the <see cref="SenderAccount"/> property
    /// </summary>
    public Type? AccountAugmenter { get; set; }

    /// <summary>
    /// Permissions belonging to the <see cref="SenderAccount"/>;
    /// populated via context augmenter
    /// </summary>
    public HashSet<string> SenderPermissions { get; init; } = [];

    /// <summary>
    /// Augmenters used to populate <see cref="SenderPermissions"/>
    /// </summary>
    public List<Type> PermissionAugmenters { get; init; } = [];

    public BotCommandContext(Bot bot, ICommand command, string fullLine)
    {
        Bot = bot;
        Command = command;
        FullLine = fullLine;

        // TODO: split out just the nickname if this is a full nick!user@host hostmask
        SenderNickname = command.Source!;
    }
}
