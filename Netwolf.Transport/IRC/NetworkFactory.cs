// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.Extensions.Logging;

using Netwolf.Transport.Commands;
using Netwolf.Transport.Events;
using Netwolf.Transport.RateLimiting;
using Netwolf.Transport.Sasl;

namespace Netwolf.Transport.IRC;

internal class NetworkFactory : INetworkFactory
{
    private ILogger<INetwork> Logger { get; init; }

    private ICommandFactory CommandFactory { get; init; }

    private IConnectionFactory ConnectionFactory { get; init; }

    private IRateLimiterFactory RateLimiterFactory { get; init; }

    private ISaslMechanismFactory SaslMechanismFactory { get; init; }

    private NetworkEvents NetworkEvents { get; init; }

    private CommandListenerRegistry CommandListenerRegistry { get; init; }

    public NetworkFactory(
        ILogger<INetwork> logger,
        ICommandFactory commandFactory,
        IConnectionFactory connectionFactory,
        IRateLimiterFactory rateLimiterFactory,
        ISaslMechanismFactory saslMechanismFactory,
        NetworkEvents networkEvents,
        CommandListenerRegistry commandListenerRegistry
        )
    {
        Logger = logger;
        CommandFactory = commandFactory;
        ConnectionFactory = connectionFactory;
        RateLimiterFactory = rateLimiterFactory;
        SaslMechanismFactory = saslMechanismFactory;
        NetworkEvents = networkEvents;
        CommandListenerRegistry = commandListenerRegistry;
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
            CommandListenerRegistry,
            NetworkEvents);
    }
}
