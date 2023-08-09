
using Microsoft.Extensions.Logging;

using Netwolf.Transport.Exceptions;
using Netwolf.Transport.Internal;

namespace Netwolf.Transport.IRC;

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
    protected ILogger<INetwork> Logger { get; init; }

    protected ICommandFactory CommandFactory { get; init; }

    protected IConnectionFactory ConnectionFactory { get; init; }

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

    private IConnection? _connection;

    /// <summary>
    /// A connection to the network
    /// </summary>
    protected IConnection Connection => _connection ?? throw new InvalidOperationException("Network is disconnected.");

    /// <summary>
    /// Network state
    /// </summary>
    private IrcState State { get; init; } = new();

    /// <summary>
    /// User-defined network name (not necessarily what the network actually calls itself)
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// True if we are currently connected to this Network
    /// </summary>
    public bool IsConnected => _connection != null && _userRegistrationCompletionSource == null;

    /// <summary>
    /// Nickname for this connection.
    /// Throws InvalidOperationException if not currently connected.
    /// </summary>
    public string Nick
    {
        get => IsConnected ? State.Nick : throw new InvalidOperationException("Network is disconnected.");
        protected set => State.Nick = value;
    }

    /// <summary>
    /// Ident for this connection.
    /// Throws InvalidOperationException if not currently connected.
    /// </summary>
    public string Ident
    {
        get => IsConnected ? State.Ident : throw new InvalidOperationException("Network is disconnected.");
        protected set => State.Ident = value;
    }

    /// <summary>
    /// Hostname for this connection.
    /// Throws InvalidOperationException if not currently connected.
    /// </summary>
    public string Host
    {
        get => IsConnected ? State.Host : throw new InvalidOperationException("Network is disconnected.");
        protected set => State.Host = value;
    }

    /// <summary>
    /// Account name for this connection, or null if not logged in.
    /// Throws InvalidOperationException if not currently connected.
    /// </summary>
    public string? Account
    {
        get => IsConnected ? State.Account : throw new InvalidOperationException("Network is disconnected.");
        protected set => State.Account = value;
    }

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
    public Network(
        string name,
        NetworkOptions options,
        ILogger<INetwork> logger,
        ICommandFactory commandFactory,
        IConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(options);

        Name = name;
        Options = options;
        Logger = logger;
        CommandFactory = commandFactory;
        ConnectionFactory = connectionFactory;

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
                _connection?.Dispose();
            }

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

                int index = Task.WaitAny(tasks.ToArray(), _messageLoopTokenSource.Token);
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
                        if (tasks[index].Status == TaskStatus.Faulted)
                        {
                            // Remote end died, close the connection
                            goto default;
                        }

                        var command = ((Task<ICommand>)tasks[index]).Result;

                        // Handle PONG specially so we can manage our timers
                        // receiving a cookie back will invalidate that timer and all timers issued prior to the cookie
                        if (command.Verb == "PONG" && command.Args.Count == 2)
                        {
                            int cookieIndex = pingTimeoutCookies.IndexOf(command.Args[1]);
                            if (cookieIndex != -1)
                            {
                                pingTimeoutTimers.RemoveRange(0, cookieIndex + 1);
                                pingTimeoutCookies.RemoveRange(0, cookieIndex + 1);
                            }
                        }

                        CommandReceived?.Invoke(command, new(this, command, _messageLoopTokenSource.Token));
                        break;
                    case 2:
                        // pingTimer fired, send a PING and reset the timer
                        // in the future we could detect other activity and hold off on sending PINGs if they're not needed
                        pingTimer = Task.Delay(Options.PingInterval, _messageLoopTokenSource.Token);
                        pingTimeoutTimers.Add(Task.Delay(Options.PingTimeout, _messageLoopTokenSource.Token));
                        string cookie = String.Format("NWPC{0:X16}", Random.Shared.NextInt64());
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
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_connection != null)
        {
            Logger.LogError("Network is already connected.");
            throw new ConnectionException("Network is already connected.");
        }

        if (Options.Servers.Count == 0)
        {
            Logger.LogError("No servers have been defined; unable to connect.");
            throw new ConnectionException("No servers have been defined; unable to connect.");
        }

        if (Options.ConnectRetries < 0)
        {
            Logger.LogError("ConnectRetries cannot be a negative number.");
            throw new ConnectionException("ConnectRetries cannot be a negative number.");
        }

        if (Options.PrimaryNick == String.Empty || Options.Ident == String.Empty)
        {
            Logger.LogError("User info for this network is not filled out.");
            throw new ConnectionException("User info for this network is not filled out.");
        }

        for (int retry = 0; retry <= Options.ConnectRetries; ++retry)
        {
            foreach (var server in Options.Servers)
            {
                using var timer = new CancellationTokenSource();
                using var aggregate = CancellationTokenSource.CreateLinkedTokenSource(timer.Token, cancellationToken);
                _connection = ConnectionFactory.Create(this, server, Options);

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

                // default to true so that if we abort prematurely, we skip to the error message
                // about the connection being aborted rather user registration timing out
                bool registrationComplete = true;

                try
                {
                    await SendRawAsync("CAP LS 302", cancellationToken);

                    if (Options.ServerPassword != null)
                    {
                        await SendAsync(
                            PrepareCommand("PASS", new string[] { Options.ServerPassword }, null),
                            cancellationToken);
                    }

                    await SendAsync(
                        PrepareCommand("NICK", new string[] { Options.PrimaryNick }, null),
                        cancellationToken);

                    // Most networks outright ignore params 2 and 3.
                    // Some follow RFC 2812 and treat param 2 as a bitfield where 4 = +w and 8 = +i.
                    // Others may allow an arbitrary user mode string in param 2 prefixed with +.
                    // For widest compatibility, leave both unspecified and just handle umodes post-registration.
                    await SendAsync(
                        PrepareCommand("USER", new string[] { Options.Ident, "0", "*", Options.RealName }),
                        cancellationToken);

                    // Handle any responses to the above. Notably, we might need to choose a new nickname,
                    // handle CAP negotiation (and SASL), or be disconnected outright. This will block the
                    // ConnectAsync method until registration is fully completed (whether successfully or not).
                    registrationComplete = Task.WaitAll(
                        new[] { _userRegistrationCompletionSource.Task },
                        (int)Options.ConnectTimeout.TotalMilliseconds,
                        cancellationToken);
                }
                catch (AggregateException ex)
                {
                    // allow other exceptions (e.g. from PrepareCommand) to bubble up and abort entirely,
                    // as those errors are not recoverable and will fail again if tried again
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
                else if (!registrationComplete)
                {
                    Logger.LogInformation("User registration timed out, trying next server in list.");
                    await _connection.DisposeAsync().ConfigureAwait(false);
                }
                else
                {
                    Logger.LogInformation("Connection aborted, trying next server in list.");
                    await _connection.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        // Getting here means we ran out of connect retries
        Logger.LogError("Unable to connect to network, maximum retries reached.");
        throw new ConnectionException("Unable to connect to network, maximum retries reached.");
    }

    /// <inheritdoc />
    public async Task DisconnectAsync(string reason, CancellationToken cancellationToken = default)
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
    public Task SendAsync(ICommand command, CancellationToken cancellationToken = default)
    {
        return Connection.SendAsync(command, cancellationToken);
    }

    private Task SendRawAsync(string command, CancellationToken cancellationToken)
    {
        return Connection.UnsafeSendAsync(command, cancellationToken);
    }

    /// <inheritdoc />
    public ICommand PrepareCommand(string verb, IEnumerable<object?>? args = null, IReadOnlyDictionary<string, object?>? tags = null)
    {
        return CommandFactory.CreateCommand(
            CommandType.Client,
            $"{State.Nick}!{State.Ident}@{State.Host}",
            verb,
            (args ?? Array.Empty<object?>()).Select(o => o?.ToString()).Where(o => o != null).ToList(),
            (tags ?? new Dictionary<string, object?>()).ToDictionary(o => o.Key, o => o.Value?.ToString()));
    }

    /// <inheritdoc />
    public ICommand[] PrepareMessage(MessageType messageType, string target, string text, IReadOnlyDictionary<string, object?>? tags = null)
    {
        var commands = new List<ICommand>();
        var messageTags = (tags ?? new Dictionary<string, object?>()).ToDictionary(o => o.Key, o => o.Value?.ToString());

        // TODO: pick the CPRIVMSG/CNOTICE variants if enabled in network options, supported by server, and we're opped on a channel shared with target;
        // this will also need to pick the relevant channel as well. CPRIVMSG target #channel :message or CNOTICE target #channel :message
        string oppedChannel = String.Empty;
        bool cprivmsgEligible = false;
        bool cnoticeEligible = false;

        string verb = (messageType, cprivmsgEligible, cnoticeEligible) switch
        {
            (MessageType.Message, false, _) => "PRIVMSG",
            (MessageType.Message, true, _) => "CPRIVMSG",
            (MessageType.Notice, _, false) => "NOTICE",
            (MessageType.Notice, _, true) => "CNOTICE",
            (_, _, _) => throw new ArgumentException("Invalid message type", nameof(messageType))
        };

        string hostmask = $"{State.Nick}!{State.Ident}@{State.Host}";
        List<string> args = new() { target };

        // :<hostmask> <verb> <target> :<text>\r\n -- 2 colons + 3 spaces + CRLF = 7 syntax characters. If CPRIVMSG/CNOTICE, one extra space is needed.
        // we build in an additional safety buffer of 14 bytes to account for cases where our hostmask is out of sync or the server adds additional context
        // to relayed messages (for 7 + 14 = 21 total bytes, leaving 491 for the rest normally or 490 when using CPRIVMSG/CNOTICE)
        int maxlen = 512 - 21 - hostmask.Length - verb.Length - target.Length;
        if (verb[0] == 'C')
        {
            maxlen -= 1 + oppedChannel.Length;
            args.Add(oppedChannel);
        }

        int lineIndex = args.Count;

        // split text if it is longer than maxlen bytes
        // TODO: if multiline is supported by the network, add appropriate tags
        var lines = UnicodeHelper.SplitText(text, maxlen);
        foreach (string line in lines)
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

    private async void UserRegistration(object? sender, NetworkEventArgs e)
    {
        if (sender is not ICommand command)
        {
            return;
        }

        switch (command.Verb)
        {
            case "001":
                State.Nick = command.Args[0];
                break;
            case "005":
                // process ISUPPORT tokens
                break;
            case "432":
            case "433":
                // primary nick didn't work for whatever reason, try secondary
                string attempted = command.Args[0];
                string secondary = Options.SecondaryNick ?? $"{Options.PrimaryNick}_";
                if (attempted == Options.PrimaryNick)
                {
                    await SendAsync(
                            PrepareCommand("NICK", new string[] { secondary }, null),
                            e.Token);
                }
                else if (attempted == secondary)
                {
                    // both taken? abort
                    Logger.LogWarning("Server rejected both primary and secondary nicks.");
                }

                break;
            case "376":
            case "422":
                // got MOTD, we've been registered
                _userRegistrationCompletionSource!.SetResult();
                break;
            case "CAP":
                // CAP negotation, figure out which subcommand we have
                break;
            case "AUTHENTICATE":
                // SASL
                break;
            case "900":
                // successful SASL, which conveniently also tells us our hostmask too
                // (avoids needing to do a /WHO on ourself to get that info)
                State.Account = command.Args[2];
                
                // enclosed in a new scope to prevent the `bits` variable from leaking out of this case
                {
                    var bits = command.Args[1].Split(new char[] { '!', '@' }, 3);

                    if (State.Ident == null)
                    {
                        State.Ident = bits[1];
                    }

                    if (State.Host == null)
                    {
                        State.Host = bits[2];
                    }
                }

                break;
            case "902":
            case "904":
            case "905":
            case "906":
                // SASL failed
                break;
        }
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
