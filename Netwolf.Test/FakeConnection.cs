using Microsoft.Extensions.Logging;

using Netwolf.Server.Commands;
using Netwolf.Transport.IRC;

using System.Security.Authentication.ExtendedProtection;

namespace Netwolf.Test;

internal class FakeConnection : IConnection
{
    private FakeServer Server { get; init; }

    private ICommandFactory CommandFactory { get; set; }

    private ICommandDispatcher CommandDispatcher { get; set; }

    private ILogger<IConnection> Logger { get; set; }

    private bool disposedValue;

    internal FakeConnection(FakeServer server, ICommandFactory commandFactory, ICommandDispatcher commandDispatcher, ILogger<IConnection> logger)
    {
        CommandFactory = commandFactory;
        CommandDispatcher = commandDispatcher;
        Server = server;
        Logger = logger;
    }

    public Task ConnectAsync(CancellationToken cancellationToken)
    {
        Server.ConnectClient(this);
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        Server.DisconnectClient(this);
        return Task.CompletedTask;
    }

    public async Task<ICommand> ReceiveAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var command = await Server.ReceiveCommand(this, cancellationToken);
        Logger.LogDebug("<-- {Command}", command.FullCommand);
        return command;
    }

    public async Task SendAsync(ICommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Logger.LogDebug("--> {Command}", command.FullCommand);
        var result = await CommandDispatcher.DispatchAsync(command, Server.State[this], cancellationToken);
        result.Send();
    }

    public Task UnsafeSendAsync(string command, CancellationToken cancellationToken)
    {
        return SendAsync(CommandFactory.Parse(CommandType.Client, command), cancellationToken);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // nothing to dispose
            }

            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    public ChannelBinding? GetChannelBinding(ChannelBindingKind kind)
    {
        return null;
    }
}
