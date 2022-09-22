using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Transport.Client
{
    public class NetworkFactory : INetworkFactory
    {
        protected Dictionary<string, Action<NetworkOptions>> Configurations { get; init; } = new Dictionary<string, Action<NetworkOptions>>();

        protected ConditionalWeakTable<IServiceProvider, INetwork> Networks { get; init; } = new ConditionalWeakTable<IServiceProvider, INetwork>();

        public IEnumerable<string> ConfiguredNetworks => Configurations.Keys;

        private IServiceProvider GlobalProvider { get; init; }

        public NetworkFactory(IServiceProvider globalProvider)
        {
            GlobalProvider = globalProvider;
        }

        public void AddNetworkConfiguration(string name, IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(configuration);
            AddNetworkConfiguration(name, options =>
            {
                configuration.Bind(options);
            });
        }

        public void AddNetworkConfiguration(string name, Action<NetworkOptions> optionsFactory)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(optionsFactory);
            Configurations.Add(name, optionsFactory);
        }

        public INetwork CreateNetworkInScope(string name, IServiceProvider provider)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(provider);
            if (ReferenceEquals(provider, GlobalProvider))
            {
                throw new ArgumentException("Networks can only be registered to Scoped DI providers", nameof(provider));
            }

            var optionFactory = Configurations[name];
            var options = new NetworkOptions();
            optionFactory(options);
            var logger = provider.GetRequiredService<ILogger<Network>>();
            var commandFactory = provider.GetRequiredService<ICommandFactory>();
            var network = new Network(name, options, logger, commandFactory);
            Networks.Add(provider, network);
            return network;
        }

        public IServiceScope CreateNetworkScope(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            var scope = GlobalProvider.CreateScope();
            CreateNetworkInScope(name, scope.ServiceProvider);
            return scope;
        }

        public AsyncServiceScope CreateAsyncNetworkScope(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            var scope = GlobalProvider.CreateAsyncScope();
            CreateNetworkInScope(name, scope.ServiceProvider);
            return scope;
        }

        public void RemoveAll()
        {
            Configurations.Clear();
        }

        public void RemoveNetworkConfiguration(string name)
        {
            Configurations.Remove(name);
        }

        INetwork INetworkFactory.GetFromScope(IServiceProvider provider)
        {
            ArgumentNullException.ThrowIfNull(provider);
            if (Networks.TryGetValue(provider, out var network))
            {
                return network;
            }

            throw new InvalidOperationException("No network was registered yet in this DI scope; use one of the INetworkFactory.Create* methods first.");
        }
    }
}
