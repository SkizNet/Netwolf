using Microsoft.Extensions.Logging;

using Netwolf.PluginFramework.Commands;
using Netwolf.Server;
using Netwolf.Server.Commands;
using Netwolf.Server.Exceptions;
using Netwolf.Server.Users;
using Netwolf.Transport.Commands;
using Netwolf.Transport.IRC;

using System.Security.Authentication.ExtendedProtection;

namespace Netwolf.Test;

internal class FakeConnection : IConnection
{
    /// <summary>
    /// If we have a Server, then we're trying to do integration tests that involve "live" connections,
    /// so this needs to operate as if it were a real connection. If Server is null, then we're not
    /// talking to any server and the connection just exists because it needs to, even though we'll never
    /// call it; in such cases always return that we're connected.
    /// </summary>
    public bool IsConnected => Server == null || Server.ClientCount > 0;

    private INetwork Network { get; init; }

    private FakeServer? Server { get; init; }

    private ICommandFactory CommandFactory { get; init; }

    private ICommandDispatcher<ICommandResponse>? CommandDispatcher { get; init; }

    private ILogger<IConnection> Logger { get; init; }

    private bool disposedValue;

    internal FakeConnection(
        INetwork network,
        FakeServer? server,
        ICommandFactory commandFactory,
        ICommandDispatcher<ICommandResponse>? commandDispatcher,
        ILogger<IConnection> logger)
    {
        Network = network;
        CommandFactory = commandFactory;
        CommandDispatcher = commandDispatcher;
        Server = server;
        Logger = logger;
    }

    public Task ConnectAsync(CancellationToken cancellationToken)
    {
        Server?.ConnectClient(Network, this);
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        Server?.DisconnectClient(Network, this);
        return Task.CompletedTask;
    }

    public async Task<ICommand> ReceiveAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Server == null || !IsConnected)
        {
            throw new InvalidOperationException("Server is null or not connected");
        }

        var command = await Server.ReceiveCommand(Network, this, cancellationToken);
        Logger.LogDebug("<-- {Command}", command.FullCommand);
        return command;
    }

    public async Task SendAsync(ICommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Server == null || !IsConnected)
        {
            throw new InvalidOperationException("Server is null or not connected");
        }

        Logger.LogDebug("--> {Command}", command.FullCommand);
        try
        {
            var context = new ServerContext() { Server = Server, User = Server.State[Network][this] };
            var result = await CommandDispatcher!.DispatchAsync(command, context, cancellationToken);
            (result ?? new NumericResponse(Server.State[Network][this], Numeric.ERR_UNKNOWNCOMMAND)).Send();
        }
        catch (CommandException ex)
        {
            ex.GetNumericResponse(Server.State[Network][this]).Send();
        }
    }

    public Task UnsafeSendRawAsync(string command, CancellationToken cancellationToken)
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
