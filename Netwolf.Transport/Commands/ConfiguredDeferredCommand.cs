// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Reactive.Linq;
using System.Runtime.CompilerServices;

namespace Netwolf.Transport.Commands;

public class ConfiguredDeferredCommand : IAsyncEnumerable<ICommand>
{
    private Func<ICommand, CancellationToken, Task> SendCallback { get; init; }

    private ICommand Command { get; init; }

    private Task? _task;
    private Task SendTask
    {
        get
        {
            _task ??= SendCallback(Command, Token);
            return _task;
        }
    }

    private IObservable<ICommand> CommandStream { get; init; }

    private CancellationToken Token { get; init; }

    internal ConfiguredDeferredCommand(Func<ICommand, CancellationToken, Task> sendCallback, ICommand command, IObservable<ICommand> commandStream, CancellationToken cancellationToken)
    {
        SendCallback = sendCallback;
        Command = command;
        Token = cancellationToken;

        CommandStream = Observable.FromAsync(() => SendTask)
            .IgnoreElements()
            // The Select() call is just to make types match up; the lambda will never be called since we ignore elements on the first stream
            .Select(_ => (ICommand)null!)
            .Concat(commandStream)
            // we want this to be a (semi-)cold observable, so ensure it is replayable
            .Replay()
            // automatically fire things off once we get our first subscriber
            .AutoConnect();
    }

    public IObservable<ICommand> ToObservable() => CommandStream.AsObservable();

    public TaskAwaiter<ICommand> GetAwaiter() => GetFinalResult().GetAwaiter();

    public ConfiguredTaskAwaitable<ICommand> ConfigureAwait(bool continueOnCapturedContext) => GetFinalResult().ConfigureAwait(continueOnCapturedContext);

    public ConfiguredTaskAwaitable<ICommand> ConfigureAwait(ConfigureAwaitOptions options) => GetFinalResult().ConfigureAwait(options);

    private async Task<ICommand> GetFinalResult()
    {
        await SendTask.ConfigureAwait(false);
        return await CommandStream.RunAsync(Token);
    }

    public IAsyncEnumerator<ICommand> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => CommandStream.ToAsyncEnumerable().GetAsyncEnumerator(CancellationTokenSource.CreateLinkedTokenSource(Token, cancellationToken).Token);
}
