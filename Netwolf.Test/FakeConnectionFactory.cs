using Netwolf.Server.Commands;
using Netwolf.Transport.Client;

namespace Netwolf.Test;

internal class FakeConnectionFactory : IConnectionFactory
{
    private FakeServer Server { get; set; }

    private ICommandFactory CommandFactory { get; set; }

    private ICommandDispatcher CommandDispatcher { get; set; }

    internal FakeConnectionFactory(FakeServer server, ICommandFactory commandFactory, ICommandDispatcher dispatcher)
    {
        CommandFactory = commandFactory;
        Server = server;
        CommandDispatcher = dispatcher;
    }

    public IConnection Create(INetwork network, IServer server, NetworkOptions options)
    {
        return new FakeConnection(Server, CommandFactory, CommandDispatcher);
    }
}
