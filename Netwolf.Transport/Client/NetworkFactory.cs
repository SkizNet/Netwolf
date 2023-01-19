using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Transport.Client
{
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
}
