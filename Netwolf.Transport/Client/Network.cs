﻿
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

        /// <summary>
        /// Cancellation token for <see cref="_messageLoop"/>, used when disposing this <see cref="Network"/> instance.
        /// </summary>
        private readonly CancellationTokenSource _messageLoopTokenSource = new();

        /// <summary>
        /// Completion source for <see cref="_messageLoop"/>, used when the connection is established or broken in normal operation.
        /// </summary>
        private TaskCompletionSource _messageLoopCompletionSource = new();

        /// <summary>
        /// Message loop for this network connection, handled in a background thread
        /// </summary>
        private readonly Task _messageLoop;

        /// <summary>
        /// Completion source used in <see cref="ConnectAsync(CancellationToken)"/> to block the function until user registration completes
        /// </summary>
        private TaskCompletionSource? _userRegistrationCompletionSource;

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
        public bool IsConnected => _connection != null && _userRegistrationCompletionSource == null;

        /// <summary>
        /// TLS client certificate used to log into the user's account.
        /// </summary>
        protected internal X509Certificate2? AccountCertificate { get; private set; }

        /// <inheritdoc />
        public event EventHandler<NetworkEventArgs>? CommandReceived;

        /// <inheritdoc />
        public event EventHandler<NetworkEventArgs>? Disconnected;

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

            // spin up the message loop for this Network
            _messageLoop = Task.Run(MessageLoop);
        }

        /// <summary>
        /// Perform cleanup of managed resources asynchronously.
        /// </summary>
        /// <returns>Awaitable ValueTask for the async cleanup operation</returns>
        protected virtual async ValueTask DisposeAsyncCore()
        {
            _messageLoopTokenSource.Cancel();
            await _messageLoop.ConfigureAwait(false);

            AccountCertificate?.Dispose();
            await NullableHelper.DisposeAsyncIfNotNull(_connection).ConfigureAwait(false);
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
                    _messageLoopTokenSource.Cancel();
                    _messageLoop.Wait();

                    AccountCertificate?.Dispose();
                    _connection?.Dispose();
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

        private void MessageLoop()
        {
            try
            {
                var tasks = new List<Task>();
                var pingTimer = Task.Delay(Options.PingInterval, _messageLoopTokenSource.Token);
                var pingTimeoutTimers = new List<Task>();
                var pingTimeoutCookies = new List<string>();

                while (true)
                {
                    tasks.Clear();
                    tasks.Add(_messageLoopCompletionSource.Task); // index 0

                    if (IsConnected)
                    {
                        tasks.Add(Connection.ReceiveAsync(_messageLoopTokenSource.Token)); // index 1
                        tasks.Add(pingTimer); // index 2
                        tasks.AddRange(pingTimeoutTimers); // index 3+
                    }
                    else if (_connection != null)
                    {
                        // in user registration (not fully connected yet, so do not send any PINGs)
                        tasks.Add(Connection.ReceiveAsync(_messageLoopTokenSource.Token)); // index 1
                    }

                    var index = Task.WaitAny(tasks.ToArray(), _messageLoopTokenSource.Token);
                    switch (index)
                    {
                        case 0:
                            // _messageLoopCompletionSource.Task fired, indicating a (dis)connection
                            // reset the source and timers
                            _messageLoopCompletionSource = new TaskCompletionSource();
                            pingTimer = Task.Delay(Options.PingInterval, _messageLoopTokenSource.Token);
                            pingTimeoutTimers.Clear();
                            pingTimeoutCookies.Clear();
                            break;
                        case 1:
                            // Connection.ReceiveAsync fired, so we have a command to dispatch
                            if (tasks[2].Status == TaskStatus.Faulted)
                            {
                                // Remote end died, close the connection
                                goto default;
                            }

                            var command = ((Task<ICommand>)tasks[2]).Result;

                            // Handle PONG specially so we can manage our timers
                            // receiving a cookie back will invalidate that timer and all timers issued prior to the cookie
                            if (command.Verb == "PONG" && command.Args.Count == 2)
                            {
                                var cookieIndex = pingTimeoutCookies.IndexOf(command.Args[1]);
                                if (cookieIndex != -1)
                                {
                                    pingTimeoutTimers.RemoveRange(0, cookieIndex + 1);
                                    pingTimeoutCookies.RemoveRange(0, cookieIndex + 1);
                                }
                            }

                            CommandReceived?.Invoke(command, new(this, command));
                            break;
                        case 2:
                            // pingTimer fired, send a PING and reset the timer
                            // in the future we could detect other activity and hold off on sending PINGs if they're not needed
                            pingTimer = Task.Delay(Options.PingInterval, _messageLoopTokenSource.Token);
                            pingTimeoutTimers.Add(Task.Delay(Options.PingTimeout, _messageLoopTokenSource.Token));
                            var cookie = String.Format("NWPC{0:X16}", Random.Shared.NextInt64());
                            pingTimeoutCookies.Add(cookie);
                            Task.Run(() => SendAsync(PrepareCommand("PING", new string[] { cookie }, null), _messageLoopTokenSource.Token));
                            break;
                        default:
                            // one of the pingTimeoutTimers fired or ReceiveAsync threw an exception
                            // this leaves the internal state "dirty" (by not cleaning up pingTimeoutTimers/Cookies, etc.)
                            // but on reconnect the completion source will be flagged and reset those
                            Task.Run(async () =>
                            {
                                if (_connection != null)
                                {
                                    await _connection.DisconnectAsync().ConfigureAwait(false);
                                    _connection = null;

                                    Disconnected?.Invoke(tasks[index].Exception, new(this));
                                }
                            });
                            break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // cancellation requested, which only happens in Dispose(), so dispose of our token source
                _messageLoopTokenSource.Dispose();
            }
        }

        /// <inheritdoc />
        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            if (_connection != null)
            {
                throw new InvalidOperationException("Network is already connected.");
            }

            while (true)
            {
                foreach (var server in Options.Servers)
                {
                    using var timer = new CancellationTokenSource();
                    using var aggregate = CancellationTokenSource.CreateLinkedTokenSource(timer.Token, cancellationToken);
                    _connection = new IrcConnection(this, server, Options, CommandFactory);

                    if (Options.ConnectTimeout != TimeSpan.Zero)
                    {
                        timer.CancelAfter(Options.ConnectTimeout);
                    }

                    try
                    {
                        Logger.LogInformation("Connecting to {server}...", server);
                        await _connection.ConnectAsync(aggregate.Token).ConfigureAwait(false);
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
                        await _connection.DisposeAsync().ConfigureAwait(false);
                        continue;
                    }

                    // Successfully connected, do user registration
                    // don't exit the loop until registration succeeds
                    // (we want to bounce/reconnect if that fails for whatever reason)
                    Logger.LogInformation("Connected to {server}.", server);

                    // set up command handlers for user registration (ensuring it is only registered once)
                    _userRegistrationCompletionSource = new TaskCompletionSource();
                    CommandReceived -= UserRegistration;
                    CommandReceived += UserRegistration;
                    Disconnected -= AbortUserRegistration;
                    Disconnected += AbortUserRegistration;

                    // alert the message loop to start processing incoming commands
                    _messageLoopCompletionSource.SetResult();

                    // wait for user registration to complete
                    try
                    {
                        await _userRegistrationCompletionSource.Task.ConfigureAwait(false);
                    }
                    catch (AggregateException ex)
                    {
                        Logger.LogDebug(ex, "An exception was thrown during user registration");
                    }

                    if (_userRegistrationCompletionSource.Task.IsCompletedSuccessfully)
                    {
                        // user registration succeeded, exit out of the connect loop
                        CommandReceived -= UserRegistration;
                        Disconnected -= AbortUserRegistration;
                        _userRegistrationCompletionSource = null;
                        return;
                    }
                    else
                    {
                        Logger.LogInformation("Connection aborted, trying next server in list.");
                    }
                }
            }
        }

        /// <inheritdoc />
        public async Task DisconnectAsync(string reason, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await SendAsync(PrepareCommand("QUIT", new string[] { reason }, null), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (_connection != null)
                {
                    await _connection.DisconnectAsync().ConfigureAwait(false);
                }

                _connection = null;
                _messageLoopCompletionSource.SetResult();
            }
        }

        /// <inheritdoc />
        public Task SendAsync(ICommand command, CancellationToken cancellationToken)
        {
            return Connection.SendAsync(command, cancellationToken);
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

        private void UserRegistration(object? sender, NetworkEventArgs e)
        {

        }

        private void AbortUserRegistration(object? sender, NetworkEventArgs e)
        {
            if (sender is AggregateException ex)
            {
                _userRegistrationCompletionSource?.SetException(ex);
            }
            else
            {
                _userRegistrationCompletionSource?.SetCanceled();
            }
        }
    }
}
