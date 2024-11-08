using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using Netwolf.Server.Commands;
using Netwolf.Server.Extensions.DependencyInjection;
using Netwolf.Transport.IRC;
using Netwolf.Transport.Extensions.DependencyInjection;
using Netwolf.PluginFramework.Commands;

namespace Netwolf.Test.Transport;

[TestClass, TestCategory("SkipWhenLiveUnitTesting")]
public class NetworkTests
{
    private ServiceProvider Container { get; init; }
    private static readonly NetworkOptions Options = new()
    {
        // keep timeouts low so tests don't take forever
        // there are some internal 5s timeouts (ident/hostname lookup) so be a bit longer than that
        ConnectTimeout = TimeSpan.FromSeconds(7),
        ConnectRetries = 0,
        PrimaryNick = "test",
        Servers = [new("irc.netwolf.org", 6697)]
    };

    public NetworkTests()
    {
        Container = new ServiceCollection()
            .AddLogging(config => config.SetMinimumLevel(LogLevel.Debug).AddConsole())
            // bring in default Netwolf DI services
            .AddTransportServices()
            .AddServerServices()
            .AddSingleton<FakeServer>()
            .Replace(ServiceDescriptor.Singleton<IConnectionFactory, FakeConnectionFactory>())
            .BuildServiceProvider();

        // add Server commands
        Container.GetRequiredService<ICommandDispatcher<ICommandResponse>>()
            .AddCommandsFromAssembly(typeof(Netwolf.Server.Network).Assembly);
    }

    [TestMethod]
    public async Task User_registration_succeeds()
    {
        using var scope = Container.CreateScope();
        var networkFactory = Container.GetRequiredService<INetworkFactory>();
        using var network = networkFactory.Create("NetwolfTest", Options);

        await network.ConnectAsync();
        Assert.IsTrue(network.IsConnected);
        Assert.AreEqual("test", network.Nick);
        Assert.AreEqual("test", network.Ident);
        Assert.AreEqual("127.0.0.1", network.Host);
    }
}
