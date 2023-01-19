using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using Netwolf.Transport.Client;
using Netwolf.Transport.Extensions.DependencyInjection;

namespace Netwolf.Test.Transport
{
    /// <summary>
    /// Tests that directly instantiate network connections
    /// </summary>
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
                .BuildServiceProvider();

            DefaultOptions = new NetworkOptions()
            {
                
            };
        }

        [TestMethod]
        public async void TestUserRegistration()
        {
            var logger = Container.GetRequiredService<ILogger<INetwork>>();
            var commandFactory = Container.GetRequiredService<ICommandFactory>();
            var connectionFactory = new FakeConnectionFactory(new FakeServer(), commandFactory);
            using var network = new Network("Test", DefaultOptions, logger, commandFactory, connectionFactory);

            await network.ConnectAsync();
        }
    }
}
