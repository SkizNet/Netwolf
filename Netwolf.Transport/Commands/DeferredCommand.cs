// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Reactive.Linq;
using System.Runtime.CompilerServices;

namespace Netwolf.Transport.Commands;

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

    private Func<ICommand, CancellationToken, Task> SendCallback { get; init; }

    private IObservable<ICommand> CommandStream { get; init; }

    private ICommand Command { get; init; }

    private bool _configured = false;

    private Task? _task;
    private Task SendTask
    {
        get
        {
            _task ??= SendCallback(Command, Token);
            return _task;
        }
    }

    private CancellationToken Token { get; init; }

    internal DeferredCommand(Func<ICommand, CancellationToken, Task> sendCallback, IObservable<ICommand> commandStream, ICommand command, CancellationToken cancellationToken)
    {
        SendCallback = sendCallback;
        CommandStream = commandStream;
        Command = command;
        Token = cancellationToken;
    }

    public ConfiguredDeferredCommand WithReply(Func<ICommand, bool> predicate)
    {
        if (_task != null)
        {
            throw new InvalidOperationException(NO_CONFIGURE_AWAITED);
        }

        if (_configured)
        {
            throw new InvalidOperationException(ALREADY_CONFIGURED);
        }

        var result = new ConfiguredDeferredCommand(SendCallback, Command, CommandStream.FirstAsync(predicate), Token);
        _configured = true;
        return result;
    }

    public ConfiguredDeferredCommand WithReplies(Func<ICommand, bool> includePredicate, Func<ICommand, bool> endPredicate)
    {
        if (_task != null)
        {
            throw new InvalidOperationException(NO_CONFIGURE_AWAITED);
        }

        if (_configured)
        {
            throw new InvalidOperationException(ALREADY_CONFIGURED);
        }

        var result = new ConfiguredDeferredCommand(SendCallback, Command, CommandStream.TakeUntil(endPredicate).Where(includePredicate), Token);
        _configured = true;
        return result;
    }

    /// <summary>
    /// Gets an awaiter that, when awaited, will send the command to the server
    /// and complete without waiting for any responses from the server.
    /// </summary>
    /// <returns></returns>
    public TaskAwaiter GetAwaiter()
    {
        if (_configured)
        {
            throw new InvalidOperationException(NO_AWAIT_CONFIGURED);
        }

        return SendTask.GetAwaiter();
    }

    public ConfiguredTaskAwaitable ConfigureAwait(bool continueOnCapturedContext)
    {
        if (_configured)
        {
            throw new InvalidOperationException(NO_AWAIT_CONFIGURED);
        }

        return SendTask.ConfigureAwait(continueOnCapturedContext);
    }

    public ConfiguredTaskAwaitable ConfigureAwait(ConfigureAwaitOptions options)
    {
        if (_configured)
        {
            throw new InvalidOperationException(NO_AWAIT_CONFIGURED);
        }

        return SendTask.ConfigureAwait(options);
    }
}
