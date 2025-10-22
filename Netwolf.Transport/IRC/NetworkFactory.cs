// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.Extensions.Logging;

using Netwolf.Transport.Commands;
using Netwolf.Transport.RateLimiting;

using System.Collections.Concurrent;
using System.Reactive.Disposables;

namespace Netwolf.Transport.IRC;

internal class NetworkFactory : INetworkFactory, INetworkRegistry
{
    private ILogger<INetwork> Logger { get; init; }

    private ICommandFactory CommandFactory { get; init; }

    private IConnectionFactory ConnectionFactory { get; init; }

    private IRateLimiterFactory RateLimiterFactory { get; init; }

    private CommandListenerRegistry CommandListenerRegistry { get; init; }

    private ConcurrentDictionary<string, INetwork?> Networks { get; init; } = [];

    public event EventHandler<INetwork>? NetworkCreated;

    public event EventHandler<INetwork>? NetworkDestroyed;

    public NetworkFactory(
        ILogger<INetwork> logger,
        ICommandFactory commandFactory,
        IConnectionFactory connectionFactory,
        IRateLimiterFactory rateLimiterFactory,
        CommandListenerRegistry commandListenerRegistry
        )
    {
        Logger = logger;
        CommandFactory = commandFactory;
        ConnectionFactory = connectionFactory;
        RateLimiterFactory = rateLimiterFactory;
        CommandListenerRegistry = commandListenerRegistry;
    }

    public INetwork Create(string name, NetworkOptions options)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!Networks.TryAdd(name, null))
        {
            throw new ArgumentException($"A network with the name '{name}' already exists.", nameof(name));
        }

        var network = new Network(
            name,
            options,
            Logger,
            CommandFactory,
            ConnectionFactory,
            RateLimiterFactory.Create(options),
            CommandListenerRegistry,
            Disposable.Create(() => RemoveNetwork(name)));

        Networks[name] = network;
        NetworkCreated?.Invoke(this, network);
        return network;
    }

    public INetwork? GetNetwork(string name)
    {
        if (Networks.TryGetValue(name, out var network))
        {
            return network;
        }

        return null;
    }

    public IEnumerable<INetwork> GetAllNetworks()
    {
        return Networks.Values.OfType<INetwork>();
    }

    private void RemoveNetwork(string name)
    {
        if (Networks.Remove(name, out var network) && network is not null)
        {
            NetworkDestroyed?.Invoke(this, network);
        }
    }
}
