using Netwolf.Transport.Client;

namespace Netwolf.Test;

internal class FakeConnection : IConnection
{
    private FakeServer Server { get; init; }

    private ICommandFactory CommandFactory { get; set; }

    private bool disposedValue;

    internal FakeConnection(FakeServer server, ICommandFactory commandFactory)
    {
        CommandFactory = commandFactory;
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

    public Task SendAsync(ICommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Server.ProcessCommand(this, command, cancellationToken);
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
