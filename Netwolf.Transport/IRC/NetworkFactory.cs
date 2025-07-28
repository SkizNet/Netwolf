// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.Extensions.Logging;

using Netwolf.Transport.Commands;
using Netwolf.Transport.Events;
using Netwolf.Transport.Extensions;
using Netwolf.Transport.RateLimiting;
using Netwolf.Transport.Sasl;

using System.Reactive.Linq;

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
        Network network = new(
            name,
            options,
            Logger,
            CommandFactory,
            ConnectionFactory,
            RateLimiterFactory.Create(options),
            SaslMechanismFactory,
            NetworkEvents);

        foreach (var listener in CommandListenerRegistry.Listeners)
        {
            // This returns IDisposable however we don't need to explicitly dispose it;
            // that will happen automatically when the Network is disposed, since disposing
            // a Subject<T> automatically unsubscribes all observers.
            _ = network.CommandReceived
                .Where(args => listener.CommandFilter.Contains(args.Command.Verb))
                .SubscribeAsync(listener.ExecuteAsync);
        }

        return network;
    }
}
