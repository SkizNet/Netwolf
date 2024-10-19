using Netwolf.PluginFramework.Commands;
using Netwolf.PluginFramework.Context;
using Netwolf.Transport.IRC;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.BotFramework.Services;

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

    /// <summary>
    /// Internal constructor to make a new BotCommandContext;
    /// called from BotCommandContextFactory
    /// </summary>
    /// <param name="bot"></param>
    /// <param name="command"></param>
    /// <param name="fullLine"></param>
    internal BotCommandContext(Bot bot, ICommand command, string fullLine)
    {
        Bot = bot;
        Command = command;
        FullLine = fullLine;

        // TODO: split out just the nickname if this is a full nick!user@host hostmask
        SenderNickname = command.Source!;
    }

    /// <summary>
    /// Copy constructor, for use in user-defined context augmenters.
    /// These augmenters may subclass BotCommandContext to add additional contextual
    /// data passed through to the command handlers.
    /// </summary>
    /// <param name="other"></param>
    public BotCommandContext(BotCommandContext other)
    {
        Bot = other.Bot;
        Command = other.Command;
        FullLine = other.FullLine;
        SenderNickname = other.SenderNickname;
        SenderAccount = other.SenderAccount;
        AccountProvider = other.AccountProvider;
        SenderPermissions = other.SenderPermissions;
        PermissionProviders = other.PermissionProviders;
    }
}
