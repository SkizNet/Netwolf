﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using Netwolf.Server.Commands;
using Netwolf.Server.Extensions.DependencyInjection;
using Netwolf.Transport.IRC;
using Netwolf.Transport.Extensions.DependencyInjection;

namespace Netwolf.Test.Transport;

[TestClass, TestCategory("SkipWhenLiveUnitTesting")]
public class NetworkTests
{
    private ServiceProvider Container { get; init; }

    public NetworkTests()
    {
        Container = new ServiceCollection()
            .AddLogging(config => config.SetMinimumLevel(LogLevel.Debug).AddConsole())
            // bring in default Netwolf DI services
            .AddTransportServices()
            .AddServerServices()
            .Replace(ServiceDescriptor.Singleton<IConnectionFactory, FakeConnectionFactory>())
            .BuildServiceProvider();
    }

    private static NetworkOptions MakeOptions(FakeServer server)
    {
        var options = new NetworkOptions()
        {
            // keep timeouts low so tests don't take forever
            // there are some internal 5s timeouts (ident/hostname lookup) so be a bit longer than that
            ConnectTimeout = TimeSpan.FromSeconds(7),
            ConnectRetries = 0,
            PrimaryNick = "test"
        };

        options.Servers.Add(server);
        return options;
    }

    [TestMethod]
    public async Task User_registration_succeeds()
    {
        using var scope = Container.CreateScope();
        var server = ActivatorUtilities.CreateInstance<FakeServer>(scope.ServiceProvider);
        var networkFactory = Container.GetRequiredService<INetworkFactory>();
        using var network = networkFactory.Create("NetwolfTest", MakeOptions(server));

        await network.ConnectAsync();
        Assert.IsTrue(network.IsConnected);
        Assert.AreEqual("test", network.Nick);
        Assert.AreEqual("test", network.Ident);
        Assert.AreEqual("127.0.0.1", network.Host);
    }
}
