using Microsoft.Extensions.DependencyInjection;

using Netwolf.PluginFramework.Commands;
using Netwolf.Server;
using Netwolf.Server.Commands;
using Netwolf.Transport.IRC;

using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;

namespace Netwolf.Test;

/// <summary>
/// Small barebones ircv3-compliant ircd with no actual network connectivity
/// Much of this code will eventually move to Netwolf.Server once I start implementing that
/// </summary>
internal class FakeServer : IServer
{
    private IServiceProvider Services { get; init; }

    internal ICommandDispatcher<ICommandResponse>? CommandDispatcher { get; init; }

    internal ConcurrentDictionary<IConnection, User> State { get; init; } = new();

    public string HostName => "irc.netwolf.org";

    public int Port => 6697;

    public bool SecureConnection => true;

    internal FakeServer(IServiceProvider services)
    {
        Services = services;
        CommandDispatcher = services.GetService<ICommandDispatcher<ICommandResponse>>();
    }

    internal FakeServer AddCommands<T>()
    {
        CommandDispatcher!.AddCommandsFromAssembly(typeof(T).Assembly);
        return this;
    }

    internal void ConnectClient(IConnection connection)
    {
        State[connection] = ActivatorUtilities.CreateInstance<User>(Services, IPAddress.Loopback, 0, 0);
    }

    internal void DisconnectClient(IConnection connection)
    {
        State.Remove(connection, out var client);
        client?.Disconnect();
    }

    internal async Task<ICommand> ReceiveCommand(IConnection client, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Task.Run(() => State[client].Queue.Take(cancellationToken)).ConfigureAwait(false);
    }
}
