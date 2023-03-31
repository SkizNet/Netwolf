using Netwolf.Transport.Client;

namespace Netwolf.Test;

internal class FakeConnectionFactory : IConnectionFactory
{
    private FakeServer Server { get; set; }

    private ICommandFactory CommandFactory { get; set; }

    internal FakeConnectionFactory(FakeServer server, ICommandFactory commandFactory)
    {
        CommandFactory = commandFactory;
        Server = server;
    }

    public IConnection Create(INetwork network, IServer server, NetworkOptions options)
    {
        return new FakeConnection(Server, CommandFactory);
    }
}
