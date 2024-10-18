using Netwolf.PluginFramework.Commands;
using Netwolf.Transport.Events;
using Netwolf.Transport.IRC;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.BotFramework;

/// <summary>
/// Represents a command that will be sent to the server once this object is awaited.
/// Before it is awaited, it can be configured to look for a specific response or
/// set of responses.
/// </summary>
public class DeferredCommand
{
    private static readonly string NO_AWAIT_CONFIGURED
        = "Cannot await an already-configured DeferredCommand. Perform operations on the object returned by WithReply() or WithReplies() instead.";

    private static readonly string NO_CONFIGURE_AWAITED
        = "Cannot configure a DeferredCommand once a Task awaiter has been created for it. Call WithReply() or WithReplies() instead of awaiting this object.";

    private static readonly string ALREADY_CONFIGURED
        = "This DeferredCommand has already been configured with a call to WithReply() or WithReplies() and cannot be configured again.";

    private Bot? Bot { get; set; }

    private ICommand Command { get; init; }

    private Task? _task;
    private Task? SendTask
    {
        get
        {
            _task ??= Bot?.InternalSendAsync(Command, Token);
            return _task;
        }
    }

    private IObservable<ICommand> CommandStream { get; init; }

    private CancellationToken Token { get; init; }

    internal DeferredCommand(Bot bot, ICommand command, CancellationToken cancellationToken)
    {
        Bot = bot;
        Command = command;
        Token = cancellationToken;
        CommandStream = Bot.CommandStream.Select(e => e.Command);
    }

    public ConfiguredDeferredCommand WithReply(Func<ICommand, bool> predicate)
    {
        if (_task != null)
        {
            throw new InvalidOperationException(NO_CONFIGURE_AWAITED);
        }

        if (Bot == null)
        {
            throw new InvalidOperationException(ALREADY_CONFIGURED);
        }

        var result = new ConfiguredDeferredCommand(Bot, Command, CommandStream.FirstAsync(predicate), Token);
        Bot = null;
        return result;
    }

    public ConfiguredDeferredCommand WithReplies(Func<ICommand, bool> includePredicate, Func<ICommand, bool> endPredicate)
    {
        if (_task != null)
        {
            throw new InvalidOperationException(NO_CONFIGURE_AWAITED);
        }

        if (Bot == null)
        {
            throw new InvalidOperationException(ALREADY_CONFIGURED);
        }

        var result = new ConfiguredDeferredCommand(Bot, Command, CommandStream.TakeUntil(endPredicate).Where(includePredicate), Token);
        Bot = null;
        return result;
    }

    /// <summary>
    /// Gets an awaiter that, when awaited, will send the command to the server
    /// and complete without waiting for any responses from the server.
    /// </summary>
    /// <returns></returns>
    public TaskAwaiter GetAwaiter()
        => SendTask?.GetAwaiter() ?? throw new InvalidOperationException(NO_AWAIT_CONFIGURED);

    public ConfiguredTaskAwaitable ConfigureAwait(bool continueOnCapturedContext)
        => SendTask?.ConfigureAwait(continueOnCapturedContext) ?? throw new InvalidOperationException(NO_AWAIT_CONFIGURED);

    public ConfiguredTaskAwaitable ConfigureAwait(ConfigureAwaitOptions options)
        => SendTask?.ConfigureAwait(options) ?? throw new InvalidOperationException(NO_AWAIT_CONFIGURED);
}
