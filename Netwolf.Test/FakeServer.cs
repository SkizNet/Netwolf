using Netwolf.Server;
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
    private delegate void CommandHandler(IConnection client, ICommand command);

    private readonly IReadOnlyDictionary<string, CommandHandler> Handlers;
    private ICommandFactory CommandFactory { get; init; }

    private bool disposedValue;

    private ConcurrentDictionary<IConnection, ClientState> State { get; init; } = new();

    internal NetworkState Config { get; init; } = new();

    internal FakeServer(ICommandFactory commandFactory)
    {
        CommandFactory = commandFactory;

        Handlers = new Dictionary<string, CommandHandler>(
            typeof(FakeServer)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .SelectMany(m => m.GetCustomAttributes<CommandAttribute>()
                .Select(a => new KeyValuePair<string, CommandHandler>(a.Command, m.CreateDelegate<CommandHandler>(this))))
            );
    }

    [Command("NICK")]
    public void OnNick(IConnection client, ICommand command)
    {
        if (command.Args.Count == 0 || command.Args[0].Length == 0)
        {
            Reply(client, null, null, Numeric.ERR_NONICKNAMEGIVEN);
            return;
        }

        string nick = command.Args[0];

        // RFC 2812 nickname validation
        if (!Regex.IsMatch(nick, @"[a-zA-Z[\]\\`_^{}|][a-zA-Z0-9[\]\\`_^{}|-]{0,15}"))
        {
            Reply(client, null, null, Numeric.ERR_ERRONEUSNICKNAME, nick);
            return;
        }

        if (State.Any(o => o.Value.Nickname == nick))
        {
            Reply(client, null, null, Numeric.ERR_NICKNAMEINUSE, nick);
            return;
        }

        State[client].Nickname = nick;

        if (!State[client].Registered)
        {
            CheckRegistrationComplete(client);
        }
        else
        {
            Reply(client, null, null, "NICK", nick);
        }
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
            Reply(client, null, null, Numeric.RPL_MYINFO, Config.ServerName, Config.Version, Config.UserModes, Config.ChannelModes, Config.ChannelModesWithParams);
            ReportISupport(client);
            OnLusers(client, CommandFactory.CreateCommand(CommandType.Client, null, "LUSERS", new List<string?>(), new Dictionary<string, string?>()));
            Reply(client, null, null, Numeric.RPL_UMODEIS, state.ModeString);
            OnMotd(client, CommandFactory.CreateCommand(CommandType.Client, null, "MOTD", new List<string?>(), new Dictionary<string, string?>()));
        }
    }

    internal void ConnectClient(IConnection connection)
    {
        State[connection] = new();
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
        var network = Config;
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

    internal Task ProcessCommand(IConnection client, ICommand command, CancellationToken cancellationToken)
    {
        if (command.CommandType != CommandType.Client)
        {
            throw new ArgumentException("Not a client command", nameof(command));
        }

        if (!Handlers.ContainsKey(command.Verb))
        {
            Reply(client, null, null, Numeric.ERR_UNKNOWNCOMMAND, command.Verb);
            return Task.CompletedTask;
        }

        Handlers[command.Verb](client, command);
        return Task.CompletedTask;
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

    [Flags]
    internal enum ChannelAccessFlags
    {
        None = 0,
        Member = 1,
        [Display(Name = "v", ShortName = "+")]
        Voice = 2,
        [Display(Name = "o", ShortName = "@")]
        Operator = 4
    }

    [Flags]
    internal enum ChannelModes : ulong
    {
        None = 0,
        [Display(Name = "m")]
        Moderated = 0x0000_0000_0000_0001,
        [Display(Name = "n")]
        NoExternalMessages = 0x0000_0000_0000_0002,
        [Display(Name = "t")]
        ProtectedTopic = 0x0000_0000_0000_0004,
        [Display(Name = "s")]
        Secret = 0x0000_0000_0000_0008,
        [Display(Name = "p")]
        Private = 0x0000_0000_0000_0010,
        [Display(Name = "i")]
        InviteOnly = 0x0000_0000_0000_0020,
        [Display(Name = "l")]
        ChannelLimit = 0x0000_0000_0000_0040,
        [Display(Name = "k")]
        Passworded = 0x0000_0000_0000_0080
    }

    internal class NetworkState
    {
        internal string NetworkName { get; set; } = "Netwolf Test";

        internal string ServerName { get; set; } = "irc.netwolf.org";

        internal string Version { get; set; } = "netwolf-0.1.0";

        internal string UserModes => "iowx";

        internal string ChannelModes => "beIiklmnostv";

        internal string ChannelModesWithParams => "beIklov";
    }

    internal class ClientState
    {
        internal readonly object ClientLock = new();

        internal BlockingCollection<ICommand> Queue { get; init; } = new();

        /// <summary>
        /// Whether the client has completed user registration or not
        /// (nothing to do with accounts)
        /// </summary>
        internal bool Registered { get; set; }

        /// <summary>
        /// If the client started CAP negotation but didn't complete it yet
        /// </summary>
        internal bool CapsPending { get; set; }

        internal string Nickname { get; set; } = null!;

        internal string Ident { get; set; } = null!;

        internal string RealHost { get; set; } = null!;

        internal string VirtualHost { get; set; } = null!;

        internal string? Account { get; set; }

        internal string RealName { get; set; } = null!;

        /// <summary>
        /// For display only
        /// </summary>
        internal string ModeString => "+";

        internal List<ChannelState> ChannelMembership { get; init; } = new();
    }

    internal class ChannelState
    {
        internal readonly object ChannelLock = new();

        internal string Name { get; set; } = null!;

        internal string Topic { get; set; } = String.Empty;

        internal string? TopicSetter { get; set; }

        internal DateTime? TopicTime { get; set; }

        internal string? Password { get; set; }

        internal int Limit { get; set; }

        internal List<string> BanList { get; init; } = new();

        internal Dictionary<ClientState, ChannelAccessFlags> Membership { get; init; } = new();
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    private class CommandAttribute : Attribute
    {
        internal string Command { get; init; }

        internal CommandAttribute(string command)
        {
            Command = command;
        }
    }
}
