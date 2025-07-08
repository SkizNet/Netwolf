using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

    internal int ClientCount = 0;

    public FakeServer(IServiceProvider services)
    {
        Services = services;
    }

    internal void ConnectClient(INetwork network, IConnection connection)
    {
        State.TryAdd(network, new());
        State[network][connection] = ActivatorUtilities.CreateInstance<User>(Services, IPAddress.Loopback, 0, 0);
        var newCount = Interlocked.Increment(ref ClientCount);
    }

    internal void DisconnectClient(INetwork network, IConnection connection)
    {
        State[network].Remove(connection, out var client);
        client?.Disconnect();
        var newCount = Interlocked.Decrement(ref ClientCount);
    }

    internal async Task<ICommand> ReceiveCommand(INetwork network, IConnection client, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!State.TryGetValue(network, out var netState) || !netState.TryGetValue(client, out var user))
        {
            throw new InvalidOperationException("The given client is not connected");
        }

        return await Task.Run(() => State[network][client].Queue.Take(cancellationToken)).ConfigureAwait(false);
    }
}
