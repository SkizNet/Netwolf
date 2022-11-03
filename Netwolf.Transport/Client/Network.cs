
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Netwolf.Transport.Internal;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Transport.Client
{
    /// <summary>
    /// A network that we connect to as a client
    /// </summary>
    public class Network : INetwork
    {
        private bool _disposed;

        /// <summary>
        /// Network options defined by the user
        /// </summary>
        protected NetworkOptions Options { get; set; }

        /// <summary>
        /// Logger for this Network
        /// </summary>
        protected ILogger<Network> Logger { get; init; }

        protected ICommandFactory CommandFactory { get; init; }

        private IrcConnection? _connection;

        /// <summary>
        /// A connection to the network
        /// </summary>
        protected IrcConnection Connection => _connection ?? throw new InvalidOperationException("Network is disconnected.");

        /// <summary>
        /// User-defined network name (not necessarily what the network actually calls itself)
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        /// True if we are currently connected to this Network
        /// </summary>
        public bool IsConnected => _connection != null;

        /// <summary>
        /// TLS client certificate used to log into the user's account.
        /// </summary>
        protected internal X509Certificate2? AccountCertificate { get; private set; }

        /// <summary>
        /// Create a new Network that can be connected to.
        /// </summary>
        /// <param name="name">
        /// Name of the network, for the caller's internal tracking purposes.
        /// The name does not need to be unique.
        /// </param>
        /// <param name="options">Network options.</param>
        /// <param name="logger">Logger to use.</param>
        public Network(string name, NetworkOptions options, ILogger<Network> logger, ICommandFactory commandFactory)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(options);

            Name = name;
            Options = options;
            Logger = logger;
            CommandFactory = commandFactory;

            if (!String.IsNullOrEmpty(Options.AccountCertificateFile))
            {
                try
                {
                    AccountCertificate = new X509Certificate2(Options.AccountCertificateFile, Options.AccountCertificatePassword);
                }
                catch (CryptographicException ex)
                {
                    Logger.LogWarning("Cannot load TLS client certificate {AccountCertificateFile}: {Message}", Options, ex);
                }
                finally
                {
                    Options.AccountCertificatePassword = null;
                }
            }
        }

        /// <summary>
        /// Perform cleanup of managed resources asynchronously.
        /// </summary>
        /// <returns>Awaitable ValueTask for the async cleanup operation</returns>
        protected virtual async ValueTask DisposeAsyncCore()
        {
            await NullableHelper.DisposeAsyncIfNotNull(_connection).ConfigureAwait(false);
            AccountCertificate?.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            Dispose(disposing: false);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    AccountCertificate?.Dispose();
                }

                AccountCertificate = null;
                _disposed = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                foreach (var server in Options.Servers)
                {
                    using var timer = new CancellationTokenSource();
                    using var aggregate = CancellationTokenSource.CreateLinkedTokenSource(timer.Token, cancellationToken);
                    var connection = new IrcConnection(this, server, Options, CommandFactory);

                    if (Options.ConnectTimeout != TimeSpan.Zero)
                    {
                        timer.CancelAfter(Options.ConnectTimeout);
                    }

                    try
                    {
                        Logger.LogInformation("Connecting to {server}...", server);
                        await connection.ConnectAsync(aggregate.Token);
                        _connection = connection;
                    }
                    catch (OperationCanceledException)
                    {
                        // if we were cancelled via the passed-in token, propagate the cancellation upwards
                        // otherwise this was a timeout, so we move on to the next server
                        if (cancellationToken.IsCancellationRequested)
                        {
                            Logger.LogInformation("Connection attempt aborted.");
                            cancellationToken.ThrowIfCancellationRequested();
                        }

                        Logger.LogInformation("Connection timed out, trying next server in list.");
                        continue;
                    }
                    finally
                    {
                        if (_connection != connection)
                        {
                            await connection.DisposeAsync();
                        }
                    }

                    // Successfully connected, do user registration
                    // don't exit the loop until registration succeeds
                    // (we want to bounce/reconnect if that fails for whatever reason)
                    Logger.LogInformation("Connected to {server}.", server);
                    
                }
            }
        }

        /// <inheritdoc />
        public async Task DisconnectAsync(string reason, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await SendAsync(PrepareCommand("QUIT", new string[] { reason }, null), cancellationToken);
            }
            finally
            {
                if (_connection != null)
                {
                    await _connection.DisconnectAsync();
                }

                _connection = null;
            }
        }

        /// <inheritdoc />
        public Task SendAsync(ICommand command, CancellationToken cancellationToken)
        {
            return Connection.SendAsync(command, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<ICommand> ReceiveAsync(CancellationToken cancellationToken)
        {
            return Connection.ReceiveAsync(cancellationToken);
        }

        /// <inheritdoc />
        public ICommand PrepareCommand(string verb, IEnumerable<object?>? args, IReadOnlyDictionary<string, object?>? tags)
        {
            return CommandFactory.CreateCommand(
                CommandType.Client,
                $"{Connection.State.Nick}!{Connection.State.Ident}@{Connection.State.Host}",
                verb,
                (args ?? Array.Empty<object?>()).Select(o => o?.ToString()).Where(o => o != null).ToList(),
                (tags ?? new Dictionary<string, object?>()).ToDictionary(o => o.Key, o => o.Value?.ToString()));
        }

        /// <inheritdoc />
        public ICommand[] PrepareMessage(MessageType messageType, string target, string text, IReadOnlyDictionary<string, object?>? tags)
        {
            var commands = new List<ICommand>();
            Dictionary<string, string?> messageTags = (tags ?? new Dictionary<string, object?>()).ToDictionary(o => o.Key, o => o.Value?.ToString());

            // TODO: pick the CPRIVMSG/CNOTICE variants if enabled in network options, supported by server, and we're opped on a channel shared with target;
            // this will also need to pick the relevant channel as well. CPRIVMSG target #channel :message or CNOTICE target #channel :message
            var oppedChannel = string.Empty;
            var cprivmsgEligible = false;
            var cnoticeEligible = false;

            var verb = (messageType, cprivmsgEligible, cnoticeEligible) switch
            {
                (MessageType.Message, false, _) => "PRIVMSG",
                (MessageType.Message, true, _) => "CPRIVMSG",
                (MessageType.Notice, _, false) => "NOTICE",
                (MessageType.Notice, _, true) => "CNOTICE",
                (_, _, _) => throw new ArgumentException("Invalid message type", nameof(messageType))
            };

            var hostmask = $"{Connection.State.Nick}!{Connection.State.Ident}@{Connection.State.Host}";
            List<string> args = new() { target };

            // :<hostmask> <verb> <target> :<text>\r\n -- 2 colons + 3 spaces + CRLF = 7 syntax characters. If CPRIVMSG/CNOTICE, one extra space is needed.
            // we build in an additional safety buffer of 14 bytes to account for cases where our hostmask is out of sync or the server adds additional context
            // to relayed messages (for 7 + 14 = 21 total bytes, leaving 491 for the rest normally or 490 when using CPRIVMSG/CNOTICE)
            var maxlen = 512 - 21 - hostmask.Length - verb.Length - target.Length;
            if (verb[0] == 'C')
            {
                maxlen -= 1 + oppedChannel.Length;
                args.Add(oppedChannel);
            }

            var lineIndex = args.Count;

            // split text if it is longer than maxlen bytes
            // TODO: if multiline is supported by the network, add appropriate tags
            var lines = UnicodeHelper.SplitText(text, maxlen);
            foreach (var line in lines)
            {
                args[lineIndex] = line;
                commands.Add(CommandFactory.CreateCommand(
                    CommandType.Client,
                    hostmask,
                    verb,
                    args,
                    messageTags
                    ));
            }

            return commands.ToArray();
        }
    }
}
