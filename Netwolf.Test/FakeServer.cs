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

    private Server.Network Network { get; init; } = new();

    internal ConcurrentDictionary<IConnection, User> State { get; init; } = new();

    internal FakeServer(ICommandFactory commandFactory)
    {
        CommandFactory = commandFactory;
    }

    [Command("USER")]
    public void OnUser(IConnection client, ICommand command)
    {
        if (State[client].Registered)
        {
            Reply(client, null, null, Numeric.ERR_ALREADYREGISTERED);
            return;
        }

        if (command.Args.Count != 4 || command.Args[0].Length == 0)
        {
            Reply(client, null, null, Numeric.ERR_NEEDMOREPARAMS, command.Verb);
            return;
        }

        State[client].Ident = $"~{command.Args[0]}";
        State[client].RealName = command.Args[3];

        CheckRegistrationComplete(client);
    }

    [Command("LUSERS")]
    public void OnLusers(IConnection client, ICommand command)
    {
        var state = State[client];

        Reply(client, null, null, Numeric.RPL_LUSERCLIENT);
        Reply(client, null, null, Numeric.RPL_LUSEROP);

        if (true /*state.HasPriv("oper:lusers:unknown")*/)
        {
            Reply(client, null, null, Numeric.RPL_LUSERUNKNOWN);
        }

        Reply(client, null, null, Numeric.RPL_LUSERCHANNELS);
        Reply(client, null, null, Numeric.RPL_LUSERME);

        // Netwolf has no concept of local vs global users, so don't give RPL_LOCALUSERS
        Reply(client, null, null, Numeric.RPL_GLOBALUSERS);
    }

    [Command("MOTD")]
    public void OnMotd(IConnection client, ICommand command)
    {
        // We do not implement or support the target parameter for this command,
        // as Netwolf does expose the individual servers comprising the network

        // TODO: support showing a real MOTD if one is set in network config
        Reply(client, null, null, Numeric.ERR_NOMOTD);
    }

    internal void CheckRegistrationComplete(IConnection client)
    {
        var state = State[client];
        if (!state.CapsPending && state.Nickname != null && state.Ident != null && state.RealName != null)
        {
            state.Registered = true;
            Reply(client, null, null, Numeric.RPL_WELCOME);
            Reply(client, null, null, Numeric.RPL_YOURHOST, state.RealHost);
            Reply(client, null, null, Numeric.RPL_CREATED);
            Reply(client, null, null, Numeric.RPL_MYINFO, Network.ServerName, Network.Version, Network.UserModes, Network.ChannelModes, Network.ChannelModesWithParams);
            ReportISupport(client);
            OnLusers(client, CommandFactory.CreateCommand(CommandType.Client, null, "LUSERS", new List<string?>(), new Dictionary<string, string?>()));
            Reply(client, null, null, Numeric.RPL_UMODEIS, state.ModeString);
            OnMotd(client, CommandFactory.CreateCommand(CommandType.Client, null, "MOTD", new List<string?>(), new Dictionary<string, string?>()));
        }
    }

    internal void ConnectClient(IConnection connection)
    {
        State[connection] = new User(Network);
    }

    internal void DisconnectClient(IConnection connection)
    {
        State.Remove(connection, out _);
    }

    private void ReportISupport(IConnection client)
    {
        // TODO: FINISH
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
