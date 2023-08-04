using Netwolf.Server.Commands;
using Netwolf.Transport.Client;

namespace Netwolf.Test;

internal class FakeConnection : IConnection
{
    private FakeServer Server { get; init; }

    private ICommandFactory CommandFactory { get; set; }

    private ICommandDispatcher CommandDispatcher { get; set; }

    private bool disposedValue;

    internal FakeConnection(FakeServer server, ICommandFactory commandFactory, ICommandDispatcher commandDispatcher)
    {
        CommandFactory = commandFactory;
        CommandDispatcher = commandDispatcher;
        Server = server;
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

    public Task<ICommand> ReceiveAsync(CancellationToken cancellationToken)
    {
        return Server.ReceiveCommand(this, cancellationToken);
    }

    public async Task SendAsync(ICommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await CommandDispatcher.DispatchAsync(command, Server.State[this], cancellationToken);

        // TODO: do things with result such as sending any response back to the client
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
                Server.Dispose();
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
}
