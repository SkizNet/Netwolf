﻿using Microsoft.Extensions.Logging;

using Netwolf.PluginFramework.Commands;
using Netwolf.Server.Commands;
using Netwolf.Transport.IRC;

namespace Netwolf.Test;

internal class FakeConnectionFactory : IConnectionFactory
{
    private ICommandFactory CommandFactory { get; set; }

    private ILogger<IConnection> ConnLogger { get; set; }

    public FakeConnectionFactory(ICommandFactory commandFactory, ILogger<IConnection> connLogger)
    {
        CommandFactory = commandFactory;
        ConnLogger = connLogger;
    }

    public IConnection Create(INetwork network, IServer server, NetworkOptions options)
    {
        return new FakeConnection((FakeServer)server, CommandFactory, ConnLogger);
    }
}
