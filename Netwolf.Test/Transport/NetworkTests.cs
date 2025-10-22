using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using Netwolf.Server.Commands;
using Netwolf.Server.Extensions.DependencyInjection;
using Netwolf.Transport.IRC;
using Netwolf.Transport.Extensions.DependencyInjection;
using Netwolf.Server;
using Microsoft.Extensions.Options;
using Netwolf.BotFramework;
using Netwolf.PluginFramework.Commands;

namespace Netwolf.Test.Transport;

[TestClass, TestCategory("SkipWhenLiveUnitTesting")]
public class NetworkTests
{
    private static NetworkOptions MakeNetworkOptions()
    {
        return new NetworkOptions()
        {
            // keep timeouts low so tests don't take forever
            // there are some internal 5s timeouts (ident/hostname lookup) so be a bit longer than that
            ConnectTimeout = TimeSpan.FromSeconds(1),
            RegistrationTimeout = TimeSpan.FromSeconds(7),
            ConnectRetries = 0,
            PrimaryNick = "test",
            Servers = [new("irc.netwolf.org", 6697)],
            UseSasl = false,
        };
    }

    private static ServiceProvider BuildContainer(ServerOptions? options = null)
    {
        options ??= new ServerOptions();

        var container = new ServiceCollection()
            .AddLogging(config => config.SetMinimumLevel(LogLevel.Debug).AddConsole())
            // bring in default Netwolf DI services
            .AddTransportServices()
            .AddServerServices()
            .AddSingleton<FakeServer>()
            .AddSingleton<IOptionsSnapshot<ServerOptions>>(new TestOptionsSnapshot<ServerOptions>() { Value = options })
            .Replace(ServiceDescriptor.Singleton<IConnectionFactory, FakeConnectionFactory>())
            .BuildServiceProvider();

        // add Server commands
        container.GetRequiredService<ICommandDispatcher<ICommandResponse>>()
            .AddCommandsFromAssembly(typeof(Netwolf.Server.Network).Assembly);

        return container;
    }

    [TestMethod]
    public async Task User_registration_succeeds()
    {
        var container = BuildContainer();
        using var scope = container.CreateScope();
        var networkFactory = container.GetRequiredService<INetworkFactory>();
        using var network = networkFactory.Create("NetwolfTest", MakeNetworkOptions());

        await network.ConnectAsync();
        var info = network.AsNetworkInfo();
        Assert.IsTrue(network.IsConnected);
        Assert.AreEqual("test", info.Nick);
        Assert.AreEqual("test", info.Ident);
        Assert.AreEqual("127.0.0.1", info.Host);
    }

    [TestMethod]
    public async Task Successfully_auth_sasl_plain()
    {
        ServerOptions serverOptions = new()
        {
            DefaultRealm = "test",
            RealmMap = new()
            {
                { "test", typeof(TestAccountProvider) }
            },
        };

        var container = BuildContainer(serverOptions);
        using var scope = container.CreateScope();
        var networkFactory = container.GetRequiredService<INetworkFactory>();
        var options = MakeNetworkOptions();
        options.UseSasl = true;
        options.AccountName = "foo";
        options.AccountPassword = "bar";
        options.AllowInsecureSaslPlain = true;

        using var network = networkFactory.Create("NetwolfTest", options);
        await network.ConnectAsync();
        var info = network.AsNetworkInfo();
        Assert.AreEqual("foo", info.Account);
    }
}
