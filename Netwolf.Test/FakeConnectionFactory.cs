using Microsoft.Extensions.Logging;

using Netwolf.PluginFramework.Commands;
using Netwolf.Server.Commands;
using Netwolf.Transport.IRC;
using Netwolf.Transport.State;

namespace Netwolf.Test;

internal class FakeConnectionFactory : IConnectionFactory
{
    private ICommandFactory CommandFactory { get; set; }

    private ICommandDispatcher<ICommandResponse>? CommandDispatcher { get; set; }

    private ILogger<IConnection> ConnLogger { get; set; }

    private FakeServer? Server { get; set; }

    // FakeServer and ICommandDispatcher may sometimes not be defined in the DI container to speed up testing that doesn't require them
    // (and to enforce that they get NullReferenceException if they're used when they shouldn't be, thus failing the test)
    public FakeConnectionFactory(
        ICommandFactory commandFactory,
        ILogger<IConnection> connLogger,
        FakeServer? server = null,
        ICommandDispatcher<ICommandResponse>? commandDispatcher = null)
    {
        Server = server;
        CommandFactory = commandFactory;
        CommandDispatcher = commandDispatcher;
        ConnLogger = connLogger;
    }

    public IConnection Create(INetwork network, ServerRecord server, NetworkOptions options)
    {
        return new FakeConnection(network, Server, CommandFactory, CommandDispatcher, ConnLogger);
    }
}
