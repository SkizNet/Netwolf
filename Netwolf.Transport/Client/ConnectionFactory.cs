﻿using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Transport.Client
{
    public class ConnectionFactory : IConnectionFactory
    {
        private ICommandFactory CommandFactory { get; init; }

        private ILogger<IConnection> Logger { get; init; }

        public ConnectionFactory(ICommandFactory commandFactory, ILogger<IConnection> logger)
        {
            CommandFactory = commandFactory;
            Logger = logger;
        }

        /// <inheritdoc/>
        public IConnection Create(INetwork network, IServer server, NetworkOptions options)
        {
            return new IrcConnection(server, options, Logger, CommandFactory);
        }
    }
}