// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.Extensions.Logging;

using Netwolf.Transport.Commands;
using Netwolf.Transport.Events;
using Netwolf.Transport.RateLimiting;
using Netwolf.Transport.Sasl;

namespace Netwolf.Transport.IRC;

public class NetworkFactory : INetworkFactory
{
    protected ILogger<INetwork> Logger { get; set; }

    protected ICommandFactory CommandFactory { get; set; }

    protected IConnectionFactory ConnectionFactory { get; set; }

    protected IRateLimiterFactory RateLimiterFactory { get; set; }

    protected ISaslMechanismFactory SaslMechanismFactory { get; set; }

    protected NetworkEvents NetworkEvents { get; set; }

    public NetworkFactory(
        ILogger<INetwork> logger,
        ICommandFactory commandFactory,
        IConnectionFactory connectionFactory,
        IRateLimiterFactory rateLimiterFactory,
        ISaslMechanismFactory saslMechanismFactory,
        NetworkEvents networkEvents)
    {
        Logger = logger;
        CommandFactory = commandFactory;
        ConnectionFactory = connectionFactory;
        RateLimiterFactory = rateLimiterFactory;
        SaslMechanismFactory = saslMechanismFactory;
        NetworkEvents = networkEvents;
    }

    public INetwork Create(string name, NetworkOptions options)
    {
        return new Network(
            name,
            options,
            Logger,
            CommandFactory,
            ConnectionFactory,
            RateLimiterFactory.Create(options),
            SaslMechanismFactory,
            NetworkEvents);
    }
}
