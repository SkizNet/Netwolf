using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Transport.Client
{
    /// <summary>
    /// Factory for <see cref="INetwork"/> that allows for runtime configuration injection
    /// </summary>
    public interface INetworkFactory
    {
        /// <summary>
        /// Retrieve the names of all networks configured in this factory
        /// </summary>
        public IEnumerable<string> ConfiguredNetworks { get; }

        /// <summary>
        /// Adds a network configuration by the specified name, using late binding
        /// </summary>
        /// <param name="name">Network name, must be unique</param>
        /// <param name="configuration">
        /// Configuration that can be bound to an instance of <see cref="NetworkOptions"/>.
        /// Binding does not happen until <see cref="CreateNetwork(String)"/> is called.
        /// </param>
        public void AddNetworkConfiguration(string name, IConfiguration configuration);

        /// <summary>
        /// Adds a network configuration by the specified name, using late binding
        /// </summary>
        /// <param name="name">Network name, must be unique</param>
        /// <param name="optionsFactory">
        /// Factory function to initialize an instance of <see cref="NetworkOptions"/>.
        /// The function is not called until <see cref="CreateNetwork(String)"/> is called.
        /// </param>
        public void AddNetworkConfiguration(string name, Action<NetworkOptions> optionsFactory);
        
        /// <summary>
        /// Removes a network configuration by the specified name
        /// </summary>
        /// <param name="name">Name to remove. If the name does not exist in the collection, nothing happens</param>
        public void RemoveNetworkConfiguration(string name);
        
        /// <summary>
        /// Removes all network configurations from the collection
        /// </summary>
        public void RemoveAll();

        /// <summary>
        /// Creates a new network DI scope with the specified name, binding a snapshot of the current <see cref="NetworkOptions"/>.
        /// <para>For use in a <c>using</c> block.</para>
        /// </summary>
        /// <param name="name">Network name to create</param>
        /// <returns>A new <see cref="IServiceScope"/> with a pre-configured <see cref="INetwork"/> ready to obtain via DI</returns>
        public IServiceScope CreateNetworkScope(string name);

        /// <summary>
        /// Creates a new network DI scope with the specified name, binding a snapshot of the current <see cref="NetworkOptions"/>.
        /// <para>For use in an <c>async using</c> block.</para>
        /// </summary>
        /// <param name="name">Network name to create</param>
        /// <returns>A new <see cref="AsyncServiceScope"/> with a pre-configured <see cref="INetwork"/> ready to obtain via DI</returns>
        public AsyncServiceScope CreateAsyncNetworkScope(string name);

        /// <summary>
        /// Add a network into an existing DI scope
        /// </summary>
        /// <param name="name">Network name to create</param>
        /// <param name="provider">Scope to associate the new network with</param>
        /// <returns>A new <see cref="INetwork"/> with a snapshot of the <see cref="NetworkOptions"/></returns>
        public INetwork CreateNetworkInScope(string name, IServiceProvider provider);

        /// <summary>
        /// Retrieves an <see cref="INetwork"/> already associated with this DI scope.
        /// This is called as part of constructor injection.
        /// </summary>
        /// <param name="provider">
        /// DI provider that already has an associated network (from <see cref="CreateNetworkInScope(String, IServiceProvider)"/> or <see cref="CreateNetworkScope(String)"/>)
        /// </param>
        /// <returns>Already-configured network; options are not re-read from configuration</returns>
        protected internal INetwork GetFromScope(IServiceProvider provider);
    }
}
