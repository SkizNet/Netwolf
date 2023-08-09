using Microsoft.Extensions.Logging;

namespace Netwolf.Transport.IRC;

public class NetworkFactory : INetworkFactory
{
    protected ILogger<INetwork> Logger { get; set; }

    protected ICommandFactory CommandFactory { get; set; }

    protected IConnectionFactory ConnectionFactory { get; set; }

    public NetworkFactory(ILogger<INetwork> logger, ICommandFactory commandFactory, IConnectionFactory connectionFactory)
    {
        Logger = logger;
        CommandFactory = commandFactory;
        ConnectionFactory = connectionFactory;
    }

    public INetwork Create(string name, NetworkOptions options)
    {
        return new Network(name, options, Logger, CommandFactory, ConnectionFactory);
    }
}
