using Netwolf.Transport.Commands;
using Netwolf.Transport.Context;

namespace Netwolf.Test.PluginFramework;

internal class TestHandler<TResult> : ICommandHandler<TResult>
{
    public string Command { get; init; }

    private TResult ReturnValue { get; init; }

    private Action? Callback { get; init; }

    internal TestHandler(string command, TResult returnValue, Action? callback = null)
    {
        Command = command;
        ReturnValue = returnValue;
        Callback = callback;
    }

    public Task<TResult> ExecuteAsync(ICommand command, IContext sender, CancellationToken cancellationToken)
    {
        if (Callback is not null)
        {
            Callback();
        }

        return Task.FromResult(ReturnValue);
    }
}
