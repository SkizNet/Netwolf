using Microsoft.Extensions.DependencyInjection;

using Netwolf.Server;
using Netwolf.Transport.Commands;
using Netwolf.Transport.IRC;

using System.Collections.Concurrent;
using System.Net;

namespace Netwolf.Test;

/// <summary>
/// Small barebones ircv3-compliant ircd with no actual network connectivity
/// Much of this code will eventually move to Netwolf.Server once I start implementing that
/// </summary>
internal class FakeServer
{
    private IServiceProvider Services { get; init; }

    internal ConcurrentDictionary<INetwork, ConcurrentDictionary<IConnection, User>> State { get; init; } = new();

    public FakeServer(IServiceProvider services)
    {
        Services = services;
    }

    internal void ConnectClient(INetwork network, IConnection connection)
    {
        State.TryAdd(network, new());
        State[network][connection] = ActivatorUtilities.CreateInstance<User>(Services, IPAddress.Loopback, 0, 0);
    }

    internal void DisconnectClient(INetwork network, IConnection connection)
    {
        State[network].Remove(connection, out var client);
        client?.Disconnect();
    }

    internal async Task<ICommand> ReceiveCommand(INetwork network, IConnection client, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Task.Run(() => State[network][client].Queue.Take(cancellationToken)).ConfigureAwait(false);
    }
}
