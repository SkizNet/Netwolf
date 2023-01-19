using Netwolf.Transport.Client;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
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

        private bool disposedValue;

        private ConcurrentDictionary<IConnection, ClientState> State { get; init; } = new();

        internal FakeServer()
        {
            Handlers = new Dictionary<string, CommandHandler>(
                typeof(FakeServer)
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                .SelectMany(m => m.GetCustomAttributes<CommandAttribute>()
                    .Select(a => new KeyValuePair<string, CommandHandler>(a.Command, m.CreateDelegate<CommandHandler>(this))))
                );
        }

        internal void ConnectClient(IConnection connection)
        {
            State[connection] = new();
        }

        internal void DisconnectClient(IConnection connection)
        {
            State.Remove(connection, out _);
        }

        internal Task ProcessCommand(IConnection client, ICommand command, CancellationToken cancellationToken)
        {
            if (command.CommandType != CommandType.Client)
            {
                throw new ArgumentException("Not a client command", nameof(command));
            }

            // run the command handler for it

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
    }
}
