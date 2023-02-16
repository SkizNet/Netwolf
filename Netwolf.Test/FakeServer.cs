using Netwolf.Transport.Client;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Netwolf.Test
{
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

            var nick = command.Args[0];

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
            Reply(client, null, null, "NICK", nick);
        }

        internal void ConnectClient(IConnection connection)
        {
            State[connection] = new();
        }

        internal void DisconnectClient(IConnection connection)
        {
            State.Remove(connection, out _);
        }

        private void Reply(IConnection client, string? source, object? tags, Numeric numeric, params string?[] args)
        {
            var description = typeof(Numeric).GetField(numeric.ToString())!.GetCustomAttributes<DisplayAttribute>().First().Description;
            Reply(client, source, tags, string.Format("{0:D3}", (int)numeric), args.Append(description).ToArray());
        }

        private void Reply(IConnection client, string? source, object? tags, string verb, params string?[] args)
        {
            var command = CommandFactory.CreateCommand(
                CommandType.Server,
                source ?? "irc.example.com",
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
        private enum ChannelAccessFlags
        {
            None = 0,
            Member = 1,
            Voice = 2,
            Operator = 4
        }

        [Flags]
        private enum ChannelModes : ulong
        {
            None = 0,
            Moderated = 0x0000_0000_0000_0001,
            NoExternalMessages = 0x0000_0000_0000_0002,
            ProtectedTopic = 0x0000_0000_0000_0004,
            Secret = 0x0000_0000_0000_0008,
            Private = 0x0000_0000_0000_0010,
            InviteOnly = 0x0000_0000_0000_0020,
            ChannelLimit = 0x0000_0000_0000_0040,
            Passworded = 0x0000_0000_0000_0080
        }

        private class ClientState
        {
            internal readonly object ClientLock = new();

            internal BlockingCollection<ICommand> Queue { get; init; } = new();

            /// <summary>
            /// Whether the client has completed user registration or not
            /// (nothing to do with accounts)
            /// </summary>
            internal bool Registered { get; set; }

            internal string Nickname { get; set; } = null!;

            internal string Ident { get; set; } = null!;

            internal string RealHost { get; set; } = null!;

            internal string VirtualHost { get; set; } = null!;

            internal string? Account { get; set; }

            internal string RealName { get; set; } = null!;

            internal List<ChannelState> ChannelMembership { get; init; } = new();
        }

        private class ChannelState
        {
            internal readonly object ChannelLock = new();

            internal string Name { get; set; } = null!;

            internal string Topic { get; set; } = string.Empty;

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

        private enum Numeric : int
        {
            [Display(Description = "Unknown command")]
            ERR_UNKNOWNCOMMAND = 421,
            [Display(Description = "No nickname given")]
            ERR_NONICKNAMEGIVEN = 431,
            [Display(Description = "Erroneus nickname")]
            ERR_ERRONEUSNICKNAME = 432,
            [Display(Description = "Nickname is already in use")]
            ERR_NICKNAMEINUSE = 433,
            [Display(Description = "Nickname collision")]
            ERR_NICKCOLLISION = 436
        }
    }
}
