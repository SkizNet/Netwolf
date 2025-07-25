// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: GPL-3.0-or-later

using Netwolf.Transport.Commands;
using Netwolf.Transport.Context;
using Netwolf.Transport.IRC;
using Netwolf.Transport.State;

namespace Netwolf.BotFramework.Services;

public class BotCommandContext : ExtensibleContextBase
{
    public override object Sender => Bot;
    public override INetworkInfo Network => Bot.NetworkInfo;
    public override ChannelRecord? Channel => Bot.NetworkInfo.GetChannel(Target);
    public override UserRecord? User => Bot.NetworkInfo.GetUserByNick(SenderNickname);

    /// <summary>
    /// Bot that received the command
    /// </summary>
    public Bot Bot { get; init; }

    /// <summary>
    /// Message target (nickname of the bot or a channel name)
    /// </summary>
    public string Target { get; init; }

    /// <summary>
    /// Parsed bot command
    /// </summary>
    public ICommand Command { get; init; }

    /// <summary>
    /// Whether this command was sent to a channel or privately to the bot
    /// </summary>
    public bool IsPrivateCommand => !Bot.NetworkInfo.ChannelTypes.Contains(Target[0]);

    /// <summary>
    /// The full PRIVMSG contents that triggered this command
    /// </summary>
    public string FullLine { get; init; }

    /// <summary>
    /// Sender's nickname
    /// </summary>
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
    /// <param name="target"></param>
    /// <param name="command"></param>
    /// <param name="fullLine"></param>
    internal BotCommandContext(Bot bot, IValidationContextFactory validationContextFactory, string target, ICommand command, string fullLine)
    {
        if (command.Source == null)
        {
            throw new ArgumentException("Command source cannot be null", nameof(command));
        }

        Bot = bot;
        ValidationContextFactory = validationContextFactory;
        Target = target;
        Command = command;
        FullLine = fullLine;
        SenderNickname = IrcUtil.SplitHostmask(command.Source).Nick;
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
        Target = other.Target;
        Command = other.Command;
        FullLine = other.FullLine;
        SenderNickname = other.SenderNickname;
        SenderAccount = other.SenderAccount;
        AccountProvider = other.AccountProvider;
        SenderPermissions = other.SenderPermissions;
        PermissionProviders = other.PermissionProviders;
    }

    /// <summary>
    /// Send a message in response to the command.
    /// </summary>
    /// <param name="message">Message to send; will be wrapped into multiple lines if long.</param>
    /// <param name="replyType">Where and how the reply will be sent</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task ReplyAsync(string message, ReplyType replyType, CancellationToken cancellationToken)
    {
        var (notice, target) = replyType switch
        {
            ReplyType.PublicMessage => (false, IsPrivateCommand ? SenderNickname : Target),
            ReplyType.PublicNotice => (true, IsPrivateCommand ? SenderNickname : Target),
            ReplyType.PrivateMessage => (false, SenderNickname),
            ReplyType.PrivateNotice => (true, SenderNickname),
            _ => throw new ArgumentException("Invalid reply type", nameof(replyType))
        };

        if (notice)
        {
            await Bot.SendNoticeAsync(target, message, cancellationToken);
        }
        else
        {
            await Bot.SendMessageAsync(target, message, cancellationToken);
        }
    }
}
