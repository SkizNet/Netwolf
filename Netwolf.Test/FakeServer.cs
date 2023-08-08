using Netwolf.Server;
using Netwolf.Server.Commands;
using Netwolf.Transport.Client;

using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Netwolf.Test;

/// <summary>
/// Small barebones ircv3-compliant ircd with no actual network connectivity
/// Much of this code will eventually move to Netwolf.Server once I start implementing that
/// </summary>
internal class FakeServer : IDisposable
{
    private ICommandFactory CommandFactory { get; init; }

    private bool disposedValue;

    private Server.Network Network { get; init; }

    internal ConcurrentDictionary<IConnection, User> State { get; init; } = new();

    internal FakeServer(ICommandFactory commandFactory, ICommandDispatcher dispatcher)
    {
        CommandFactory = commandFactory;
        Network = new(commandFactory, dispatcher);
    }

    internal void ConnectClient(IConnection connection)
    {
        State[connection] = new User(Network);
    }

    internal void DisconnectClient(IConnection connection)
    {
        State.Remove(connection, out _);
    }

    private void Reply(IConnection client, string? source, object? tags, Numeric numeric, params string?[] args)
    {
        var user = State[client];
        var network = Network;
        string? description = typeof(Numeric).GetField(numeric.ToString())!.GetCustomAttributes<DisplayAttribute>().FirstOrDefault()?.Description;
        var realArgs = new List<string?>() { user.Nickname };
        realArgs.AddRange(args);
        if (description != null)
        {
            realArgs.Add(description.Interpolate(user, network));
        }

        Reply(client, source, tags, String.Format("{0:D3}", (int)numeric), realArgs.ToArray());
    }

    private void Reply(IConnection client, string? source, object? tags, string verb, params string?[] args)
    {
        var command = CommandFactory.CreateCommand(
            CommandType.Server,
            source ?? "irc.netwolf.org",
            verb.ToUpperInvariant(),
            args.ToList(),
            tags?.GetType().GetProperties().ToDictionary(o => o.Name, o => o.GetValue(tags)?.ToString()) ?? new Dictionary<string, string?>());
        State[client].Queue.Add(command);
    }

    internal async Task<ICommand> ReceiveCommand(IConnection client, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Task.Run(() => State[client].Queue.Take(cancellationToken)).ConfigureAwait(false);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
            }

            disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
