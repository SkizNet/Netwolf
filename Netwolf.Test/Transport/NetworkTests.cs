using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Netwolf.Server.Extensions.DependencyInjection;
using Netwolf.Transport.Client;
using Netwolf.Transport.Extensions.DependencyInjection;

namespace Netwolf.Test.Transport;

[TestClass]
public class NetworkTests
{
    private ServiceProvider Container { get; init; }

    private NetworkOptions DefaultOptions { get; init; }

    public NetworkTests()
    {
        Container = new ServiceCollection()
            .AddLogging(config => config.AddConsole())
            // bring in default Netwolf DI services
            .AddTransportServices()
            .AddServerServices()
            .BuildServiceProvider();

        DefaultOptions = new NetworkOptions()
        {
            // there's no actual sockets being opened here;
            // keep timeouts low so tests don't take forever
            ConnectTimeout = TimeSpan.FromSeconds(5),
            ConnectRetries = 0
        };

        DefaultOptions.Servers.Add(new Netwolf.Transport.Client.Server("irc.netwolf.org", 6667));
    }

    [TestMethod]
    public async Task TestUserRegistration()
    {
        var logger = Container.GetRequiredService<ILogger<INetwork>>();
        var commandFactory = Container.GetRequiredService<ICommandFactory>();
        var connectionFactory = new FakeConnectionFactory(new FakeServer(commandFactory), commandFactory);
        using var network = new Network("Test", DefaultOptions, logger, commandFactory, connectionFactory);

        await network.ConnectAsync();
        Assert.IsTrue(true);
    }
}
