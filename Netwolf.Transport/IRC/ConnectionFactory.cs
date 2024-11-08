// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.Extensions.Logging;

using Netwolf.Transport.State;

namespace Netwolf.Transport.IRC;

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
    public IConnection Create(INetwork network, ServerRecord server, NetworkOptions options)
    {
        return new IrcConnection(server, options, Logger, CommandFactory);
    }
}
