﻿
// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.Extensions.Logging;

using Netwolf.PluginFramework.Commands;
using Netwolf.Transport.Events;
using Netwolf.Transport.Exceptions;
using Netwolf.Transport.Internal;
using Netwolf.Transport.Sasl;

using System.Collections.Immutable;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Security.Authentication.ExtendedProtection;
using System.Text;

using static Netwolf.Transport.IRC.INetwork;

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

    protected ISaslMechanismFactory SaslMechanismFactory { get; init; }

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

    /// <summary>
    /// Server we are connected to, null if not connected
    /// </summary>
    private IServer? Server { get; set; }

    private IConnection? _connection;

    /// <summary>
    /// A connection to the network
    /// </summary>
    protected IConnection Connection
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _connection ?? throw new InvalidOperationException("Network is disconnected.");
        }
    }

    /// <summary>
    /// Network state
    /// </summary>
    private IrcState State { get; set; }

    /// <summary>
    /// User-defined network name (not necessarily what the network actually calls itself)
    /// </summary>
    public string Name => State.Name;

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
        protected set => State = State with { Nick = value };
    }

    /// <summary>
    /// Ident for this connection.
    /// Throws InvalidOperationException if not currently connected.
    /// </summary>
    public string Ident
    {
        get => IsConnected ? State.Ident : throw new InvalidOperationException("Network is disconnected.");
        protected set => State = State with { Ident = value };
    }

    /// <summary>
    /// Hostname for this connection.
    /// Throws InvalidOperationException if not currently connected.
    /// </summary>
    public string Host
    {
        get => IsConnected ? State.Host : throw new InvalidOperationException("Network is disconnected.");
        protected set => State = State with { Host = value };
    }

    /// <summary>
    /// Account name for this connection, or null if not logged in.
    /// Throws InvalidOperationException if not currently connected.
    /// </summary>
    public string? Account
    {
        get => IsConnected ? State.Account : throw new InvalidOperationException("Network is disconnected.");
        protected set => State = State with { Account = value };
    }

    /// <summary>
    /// Maxmium length of an IRC line (excluding tags, including final CRLF).
    /// </summary>
    private int MaxLength { get; set; } = 512;

    /// <summary>
    /// Internal event stream for Command events
    /// </summary>
    private readonly Subject<CommandEventArgs> _commandEventStream = new();

    /// <inheritdoc />
    public IObservable<CommandEventArgs> CommandReceived => _commandEventStream.AsObservable();

    /// <inheritdoc />
    public event EventHandler<NetworkEventArgs>? Disconnected;

    /// <summary>
    /// Internal event stream for CAP events
    /// </summary>
    private readonly Subject<CapEventArgs> _capEventStream = new();

    /// <summary>
    /// Values for all caps received via CAP LS or CAP NEW, including those we did not enable
    /// </summary>
    private readonly Dictionary<string, string?> _capValueCache = [];

    /// <inheritdoc />
    public CapFilter? ShouldEnableCap { get; set; }

    /// <inheritdoc />
    public IObservable<CapEventArgs> CapEnabled => from e in _capEventStream
                                                   where e.Subcommand == "ACK"
                                                   select e;

    /// <inheritdoc />
    public IObservable<CapEventArgs> CapDisabled => from e in _capEventStream
                                                    where e.Subcommand == "DEL"
                                                    select e;

    /// <summary>
    /// SASL mechanisms that are supported by both the server and us.
    /// We will try these in order of most to least secure.
    /// If the server doesn't support CAP version 302, this will only be the mechanisms
    /// selected by the client and doesn't necessarily speak to server support.
    /// Mechanisms will be removed from this set as they are tried so that we don't re-try them.
    /// </summary>
    private HashSet<string> SaslMechs { get; set; } = [];

    /// <summary>
    /// SASL mechanism that is currently in use, or <c>null</c> if SASL isn't being attempted or failed
    /// </summary>
    private ISaslMechanism? SelectedSaslMech { get; set; }

    /// <summary>
    /// Maximum length of SaslBuffer (64 KiB base64-encoded, 48 KiB after decoding).
    /// We abort SASL if the server sends more data than this
    /// </summary>
    private const int SASL_BUFFER_MAX_LENGTH = 65536;

    /// <summary>
    /// Buffer 
    /// </summary>
    private StringBuilder SaslBuffer { get; set; } = new();

    /// <summary>
    /// If true, suspend sending CAP END when receiving an ACK until SASL finishes
    /// </summary>
    public bool SuspendCapEndForSasl { get; set; } = false;

    private static readonly string[] _END = ["END"];
    private static readonly string[] _STAR = ["*"];
    private static readonly string[] _PLUS = ["+"];

    /// <summary>
    /// Create a new Network that can be connected to.
    /// </summary>
    /// <param name="name">
    /// Name of the network, for the caller's internal tracking purposes.
    /// The name does not need to be unique.
    /// </param>
    /// <param name="options">Network options.</param>
    /// <param name="logger">Logger to use.</param>
    /// <param name="commandFactory"></param>
    /// <param name="connectionFactory"></param>
    /// <param name="saslMechanismFactory"></param>
    public Network(
        string name,
        NetworkOptions options,
        ILogger<INetwork> logger,
        ICommandFactory commandFactory,
        IConnectionFactory connectionFactory,
        ISaslMechanismFactory saslMechanismFactory)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(options);

        State = new(name, options.PrimaryNick, options.Ident, string.Empty, null, ImmutableDictionary<string, string?>.Empty, ImmutableHashSet<string>.Empty, ImmutableDictionary<ISupportToken, string?>.Empty);
        Options = options;
        Logger = logger;
        CommandFactory = commandFactory;
        ConnectionFactory = connectionFactory;
        SaslMechanismFactory = saslMechanismFactory;

        // spin up the message loop for this Network
        _messageLoop = Task.Run(MessageLoop);
    }

    /// <summary>
    /// Perform cleanup of managed resources asynchronously.
    /// </summary>
    /// <returns>Awaitable ValueTask for the async cleanup operation</returns>
    protected virtual async ValueTask DisposeAsyncCore()
    {
        _capEventStream.Dispose();
        _messageLoopTokenSource.Cancel();
        await _messageLoop.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        _messageLoopTokenSource.Dispose();
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
                _capEventStream.Dispose();
                _messageLoopTokenSource.Cancel();
                _messageLoop.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing).GetAwaiter().GetResult();
                _messageLoopTokenSource.Dispose();
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
        var tasks = new List<Task>();
        var token = _messageLoopTokenSource.Token;
        var pingTimer = Task.Delay(Options.PingInterval, token);
        var pingTimeoutTimers = new List<Task>();
        var pingTimeoutCookies = new List<string>();

        while (true)
        {
            tasks.Clear();
            tasks.Add(_messageLoopCompletionSource.Task); // index 0

            if (IsConnected)
            {
                tasks.Add(Connection.ReceiveAsync(token)); // index 1
                tasks.Add(pingTimer); // index 2
                tasks.AddRange(pingTimeoutTimers); // index 3+
            }
            else if (_connection != null)
            {
                // in user registration (not fully connected yet, so do not send any PINGs)
                tasks.Add(Connection.ReceiveAsync(token)); // index 1
            }

            int index = Task.WaitAny([.. tasks], token);
            switch (index)
            {
                case 0:
                    // _messageLoopCompletionSource.Task fired, indicating a (dis)connection
                    // reset the source and timers
                    _messageLoopCompletionSource = new TaskCompletionSource();
                    pingTimer = Task.Delay(Options.PingInterval, token);
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

                    try
                    {
                        OnCommandReceived(command, token);
                        _commandEventStream.OnNext(new(this, command, token));
                    }
                    catch (Exception e)
                    {
                        // if a command callback failed due to an exception, don't crash the message loop
                        Logger.LogError(e, "An error occurred while handling a {Command} from the server", command.Verb);
                    }
                        
                    break;
                case 2:
                    // pingTimer fired, send a PING and reset the timer
                    // in the future we could detect other activity and hold off on sending PINGs if they're not needed
                    pingTimer = Task.Delay(Options.PingInterval, token);
                    pingTimeoutTimers.Add(Task.Delay(Options.PingTimeout, token));
                    string cookie = String.Format("NWPC{0:X16}", Random.Shared.NextInt64());
                    pingTimeoutCookies.Add(cookie);
                    _ = SendAsync(PrepareCommand("PING", [cookie], null), token);
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

                            Disconnected?.Invoke(this, new(this, tasks[index].Exception));
                        }
                    });
                    break;
            }
        }
    }

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

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
                Server = server;

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

                // set up disconnection handler for user registration (ensuring it is only registered once)
                _userRegistrationCompletionSource = new TaskCompletionSource();
                Disconnected -= AbortUserRegistration;
                Disconnected += AbortUserRegistration;

                // alert the message loop to start processing incoming commands
                _messageLoopCompletionSource.SetResult();

                // default to true so that if we abort prematurely, we skip to the error message
                // about the connection being aborted rather user registration timing out
                bool registrationComplete = true;

                try
                {
                    _capValueCache.Clear();
                    await UnsafeSendRawAsync("CAP LS 302", cancellationToken);

                    if (Options.ServerPassword != null)
                    {
                        await SendAsync(
                            PrepareCommand("PASS", [Options.ServerPassword], null),
                            cancellationToken);
                    }

                    await SendAsync(
                        PrepareCommand("NICK", [Options.PrimaryNick], null),
                        cancellationToken);

                    // Most networks outright ignore params 2 and 3.
                    // Some follow RFC 2812 and treat param 2 as a bitfield where 4 = +w and 8 = +i.
                    // Others may allow an arbitrary user mode string in param 2 prefixed with +.
                    // For widest compatibility, leave both unspecified and just handle umodes post-registration.
                    await SendAsync(
                        PrepareCommand("USER", [Options.Ident, "0", "*", Options.RealName]),
                        cancellationToken);

                    // Handle any responses to the above. Notably, we might need to choose a new nickname,
                    // handle CAP negotiation (and SASL), or be disconnected outright. This will block the
                    // ConnectAsync method until registration is fully completed (whether successfully or not).
                    registrationComplete = Task.WaitAll(
                        [_userRegistrationCompletionSource.Task],
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
    public async Task DisconnectAsync(string reason)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            await SendAsync(PrepareCommand("QUIT", [reason], null)).ConfigureAwait(false);
        }
        finally
        {
            if (_connection != null)
            {
                await _connection.DisconnectAsync().ConfigureAwait(false);
            }

            _connection = null;
            Server = null;
            _messageLoopCompletionSource.SetResult();
        }

        Disconnected?.Invoke(this, new(this, default));
    }

    /// <inheritdoc />
    public Task SendAsync(ICommand command, CancellationToken cancellationToken = default)
    {
        return Connection.SendAsync(command, cancellationToken);
    }

    /// <inheritdoc />
    public Task UnsafeSendRawAsync(string command, CancellationToken cancellationToken)
    {
        return Connection.UnsafeSendRawAsync(command, cancellationToken);
    }

    /// <inheritdoc />
    public ICommand PrepareCommand(string verb, IEnumerable<object?>? args = null, IReadOnlyDictionary<string, string?>? tags = null)
    {
        return CommandFactory.CreateCommand(
            CommandType.Client,
            $"{State.Nick}!{State.Ident}@{State.Host}",
            verb,
            (args ?? []).Select(o => o?.ToString()).Where(o => o != null).ToList(),
            (tags ?? new Dictionary<string, string?>()).ToDictionary(),
            new(MaxLength));
    }

    /// <inheritdoc />
    public ICommand[] PrepareMessage(MessageType messageType, string target, string text, IReadOnlyDictionary<string, string?>? tags = null, string? sharedChannel = null)
    {
        var commands = new List<ICommand>();

        string verb = messageType switch
        {
            MessageType.Message => "PRIVMSG",
            MessageType.Notice => "NOTICE",
            _ => throw new ArgumentException("Invalid message type", nameof(messageType))
        };

        string hostmask = $"{State.Nick}!{State.Ident}@{State.Host}";
        List<string> args = [target];

        // :<hostmask> <verb> <target> :<text>\r\n -- 2 colons + 3 spaces + CRLF = 7 syntax characters. If CPRIVMSG/CNOTICE, one extra space is needed.
        // we build in an additional safety buffer of 14 bytes to account for cases where our hostmask is out of sync or the server adds additional context
        // to relayed messages (for 7 + 14 = 21 total bytes, leaving 491 for the rest normally or 490 when using CPRIVMSG/CNOTICE)
        int maxlen = MaxLength - 21 - hostmask.Length - verb.Length - target.Length;

        // If CPRIVMSG/CONTICE is enabled by the ircd and a sharedChannel was given, use it
        if (sharedChannel != null)
        {
            string cVerb = "C" + verb;
            if (State.ISupport.ContainsKey(new(cVerb)))
            {
                verb = cVerb;
                maxlen -= 1 + sharedChannel.Length;
                args.Add(sharedChannel);
            }
        }

        int lineIndex = args.Count;

        // split text if it is longer than maxlen bytes
        bool multilineEnabled = TryGetEnabledCap("draft/multiline", out var multilineValue) && State.EnabledCaps.Contains("batch");
        Dictionary<string, int> multilineLimits = [];
        if (multilineValue != null)
        {
            // coerce invalid or missing values to -1, which will disable multiline if this happens to be on max-bytes or max-lines
            multilineLimits = multilineValue.Split(',').Select(t => t.Split('=')).ToDictionary(a => a[0], a => int.TryParse(a.ElementAtOrDefault(1), out int i) ? i : -1);
        }

        multilineLimits.TryAdd("max-bytes", int.MaxValue);
        multilineLimits.TryAdd("max-lines", int.MaxValue);

        // did the server give us stupid limits? if so disable multiline
        if (multilineLimits["max-bytes"] <= maxlen || multilineLimits["max-lines"] <= 1)
        {
            multilineEnabled = false;
        }

        var lines = UnicodeHelper.SplitText(text, maxlen, false);
        CommandCreationOptions options = new(MaxLength);
        string batchId = Guid.NewGuid().ToString();
        int batchLines = 0;
        int batchBytes = -1; // our algorithm incorrectly credits an additional byte for a \n before the first line of the batch since isConcat is false for the first line
        bool isConcat = false;
        ImmutableDictionary<string, string?> tagsBase = tags != null ? ImmutableDictionary.CreateRange(tags) : ImmutableDictionary<string, string?>.Empty;
        ImmutableDictionary<string, string?> tagsFinal = tagsBase;

        foreach (var (line, isHardBreak) in lines)
        {
            if (multilineEnabled)
            {
                int byteCount = Encoding.UTF8.GetByteCount(args[^1]) + (isConcat ? 0 : 1);

                // do we need to start a new batch?
                if (batchLines == 0 || batchLines + 1 > multilineLimits["max-lines"] || batchBytes + byteCount > multilineLimits["max-bytes"])
                {
                    // do we need to end a previous batch?
                    if (batchLines != 0)
                    {
                        commands.Add(CommandFactory.CreateCommand(CommandType.Client, hostmask, "BATCH", [$"-{batchId}"], tagsBase, options));
                        batchId = Guid.NewGuid().ToString();
                        batchLines = 0;
                        batchBytes = -1; // see previous comment on batchBytes for why we start at -1
                    }

                    commands.Add(CommandFactory.CreateCommand(CommandType.Client, hostmask, "BATCH", [$"+{batchId}", "draft/multiline", target], tagsBase, options));
                    isConcat = false;
                }

                tagsFinal = tagsBase.SetItem("batch", batchId);
                if (isConcat)
                {
                    tagsFinal = tagsFinal.SetItem("draft/multiline-concat", null);
                }
                else
                {
                    tagsFinal = tagsFinal.Remove("draft/multiline-concat");
                }

                batchLines++;
                batchBytes += byteCount;
                isConcat = !isHardBreak;
            }

            args[lineIndex] = line;
            commands.Add(CommandFactory.CreateCommand(CommandType.Client, hostmask, verb, args, tagsFinal, options));
        }

        if (multilineEnabled)
        {
            // end the final batch
            commands.Add(CommandFactory.CreateCommand(CommandType.Client, hostmask, "BATCH", [$"-{batchId}"], tagsBase, options));
        }

        return [.. commands];
    }

    private void OnCommandReceived(ICommand command, CancellationToken cancellationToken)
    {
        if (!IsConnected)
        {
            // callbacks we only handle if we're pre-registration
            switch (command.Verb)
            {
                case "001":
                    Nick = command.Args[0];
                    break;
                case "432":
                case "433":
                    // primary nick didn't work for whatever reason, try secondary
                    string attempted = command.Args[0];
                    string secondary = Options.SecondaryNick ?? $"{Options.PrimaryNick}_";
                    if (attempted == Options.PrimaryNick)
                    {
                        _ = SendAsync(PrepareCommand("NICK", [secondary], null), cancellationToken);
                    }
                    else if (attempted == secondary)
                    {
                        // both taken? abort
                        Logger.LogWarning("Server rejected both primary and secondary nicks.");
                    }

                    break;
                case "376":
                case "422":
                    // got MOTD, we've been registered. But we might still not know our own ident/host,
                    // so send out a WHO for ourselves before handing control back to client
                    _ = SendAsync(PrepareCommand("WHO", [State.Nick]), cancellationToken);
                    break;
                case "352":
                    State = State with { Ident = command.Args[2], Host = command.Args[3] };
                    break;
                case "315":
                    // end of WHO, so we've pulled our own client details and can hand back control
                    _userRegistrationCompletionSource!.SetResult();
                    break;
                case "410":
                    // CAP command failed, bail out
                    // although if it's saying our CAP END failed (broken ircd), don't cause an infinite loop
                    if (command.Args[1] != "END")
                    {
                        _ = SendAsync(PrepareCommand("CAP", _END), cancellationToken);
                    }

                    break;
            }
        }

        // callbacks we always handle, even post-registration
        switch (command.Verb)
        {
            case "005":
                // process ISUPPORT tokens
                // first arg is our nick and last arg is the trailing "is supported by this server" so omit both
                Dictionary<ISupportToken, string?> newISupport = [];
                for (int i = 1; i < command.Args.Count - 1; i++)
                {
                    var token = command.Args[i];
                    string? value = null;
                    if (token.Contains('='))
                    {
                        var splitToken = token.Split('=', 2);
                        token = splitToken[0];
                        value = splitToken[1];
                    }

                    newISupport[new(token)] = value;

                    // we process some tokens here too because we need the info
                    switch (token)
                    {
                        case "LINELEN":
                            if (int.TryParse(value, out int lineLen))
                            {
                                MaxLength = Math.Max(lineLen, MaxLength);
                            }
                            break;
                    }
                }

                State = State with { ISupport = State.ISupport.SetItems(newISupport) };
                break;
            case "CAP":
                // CAP negotation
                OnCapCommand(command, cancellationToken);
                break;
            case "AUTHENTICATE":
                // SASL
                OnSaslCommand(command, cancellationToken);
                break;
            case "900":
                // successful SASL, record our account name
                Account = command.Args[2];
                break;
            case "904":
            case "905":
                // SASL failed, retry with next mech
                break;
            case "902":
            case "906":
            case "907":
                // SASL failed, don't retry
                break;
            case "908":
                // failed, but will also get a 904, simply update supported mechs
                break;
            case "PING":
                // send a PONG
                _ = SendAsync(PrepareCommand("PONG", [command.Args[0]]), cancellationToken);
                break;
        }
    }

    private void OnCapCommand(ICommand command, CancellationToken cancellationToken)
    {
        // CAPs that are always enabled if supported by the server, because we support them in this layer
        HashSet<string> defaultCaps =
        [
            "batch",
            "cap-notify",
            "draft/multiline",
            "message-ids",
            "message-tags",
            "server-time",
        ];

        // figure out which subcommand we have
        // CAP nickname subcommand args...
        switch (command.Args[1])
        {
            case "LS":
            case "NEW":
                {
                    string caps;
                    bool final = false;
                    Dictionary<string, string?> newSupportedCaps = [];
                    // request supported CAPs; might be multi-line so don't act until we get everything
                    if (command.Args[1] == "LS" && command.Args[2] == "*")
                    {
                        // multiline
                        caps = command.Args[3];
                    }
                    else
                    {
                        // final LS
                        caps = command.Args[2];
                        final = true;
                    }

                    foreach (var cap in caps.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    {
                        string key = cap;
                        string? value = null;

                        if (cap.Contains('='))
                        {
                            var bits = cap.Split('=', 2);
                            key = bits[0];
                            value = bits[1];
                        }

                        newSupportedCaps[key] = value;
                    }

                    State = State with { SupportedCaps = State.SupportedCaps.SetItems(newSupportedCaps) };

                    if (final)
                    {
                        List<string> request = [];

                        foreach (var (key, value) in State.SupportedCaps)
                        {
                            _capValueCache[key] = value;

                            bool shouldEnable = defaultCaps.Contains(key);
                            if (!shouldEnable && ShouldEnableCap != null)
                            {
                                var args = new CapEventArgs(this, key, value, command.Args[1]);
                                shouldEnable = ShouldEnableCap.GetInvocationList().Cast<CapFilter>().Any(f => f(args));
                            }

                            if (key == "sasl" && Options.UseSasl && State.Account == null)
                            {
                                // negotiate SASL
                                HashSet<string> supportedSaslTypes = new(SaslMechanismFactory.GetSupportedMechanisms(Options, Server!));

                                if (value != null)
                                {
                                    supportedSaslTypes.IntersectWith(value.Split(','));
                                }

                                supportedSaslTypes.ExceptWith(Options.DisabledSaslMechs);

                                if (value == null || supportedSaslTypes.Count != 0)
                                {
                                    request.Add("sasl");
                                    SaslMechs = supportedSaslTypes;
                                }
                            }
                            else if (shouldEnable)
                            {
                                request.Add(key);
                            }
                        }

                        // handle extremely large cap sets by breaking into multiple CAP REQ commands;
                        // we want to ensure the server's response (ACK or NAK) fits within 512 bytes with protocol overhead
                        // :server CAP nick ACK :data\r\n -- 14 bytes of overhead (leaving 498), plus nicklen, plus serverlen
                        // we reserve another 64 bytes just in case there is other unexpected overhead. better to send an extra
                        // CAP REQ than to be rejected because the server reply is longer than we anticipated it'd be
                        // Note: don't use MaxLength here since we're still pre-registration and haven't received ISUPPORT
                        int maxBytes = 434 - (State.Nick?.Length ?? 1) - (command.Source?.Length ?? 0);
                        int consumedBytes = 0;
                        List<string> param = [];

                        foreach (var token in request)
                        {
                            if (consumedBytes + token.Length > maxBytes)
                            {
                                _ = SendAsync(PrepareCommand("CAP", ["REQ", String.Join(" ", param)]), cancellationToken);
                                consumedBytes = 0;
                                param.Clear();
                            }

                            param.Add(token);
                            consumedBytes += token.Length;
                        }

                        if (param.Count > 0)
                        {
                            _ = SendAsync(PrepareCommand("CAP", ["REQ", String.Join(" ", param)]), cancellationToken);
                        }
                        else if (!IsConnected)
                        {
                            // we don't support any of the server's caps, so end cap negotiation here
                            _ = SendAsync(PrepareCommand("CAP", _END), cancellationToken);
                        }
                    }
                }

                break;
            case "ACK":
            case "LIST":
                {
                    // mark CAPs as enabled client-side, then finish cap negotiation if this was an ACK (not a LIST)
                    string[] newCaps = command.Args[2].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    State = State with { EnabledCaps = State.EnabledCaps.Union(newCaps) };

                    foreach (var cap in newCaps)
                    {
                        var args = new CapEventArgs(this, cap, _capValueCache.GetValueOrDefault(cap), command.Args[1]);
                        _capEventStream.OnNext(args);
                    }

                    if (command.Args[1] == "ACK" && newCaps.Contains("sasl"))
                    {
                        // if we're not registered yet, suspend registration until SASL finishes
                        SuspendCapEndForSasl = !IsConnected;
                        AttemptSasl(cancellationToken);
                    }

                    if (command.Args[1] == "ACK" && !IsConnected && !SuspendCapEndForSasl)
                    {
                        _ = SendAsync(PrepareCommand("CAP", _END), cancellationToken);
                    }
                }

                break;
            case "DEL":
                {
                    // mark CAPs as disabled client-side if applicable; don't send CAP END in any event here
                    string[] removedCaps = command.Args[2].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    State = State with { EnabledCaps = State.EnabledCaps.Except(removedCaps) };

                    foreach (var cap in removedCaps)
                    {
                        var args = new CapEventArgs(this, cap, _capValueCache.GetValueOrDefault(cap), command.Args[1]);
                        _capEventStream.OnNext(args);
                    }
                }

                break;
            case "NAK":
                // we couldn't set CAPs, bail out
                if (!IsConnected)
                {
                    _ = SendAsync(PrepareCommand("CAP", _END), cancellationToken);
                }

                break;
            default:
                // not something we recognize; log but otherwise ignore
                Logger.LogInformation("Received unrecognized CAP {Command} from server (potentially broken ircd)", command.Args[1]);
                break;
        }
    }

    private void AttemptSasl(CancellationToken cancellationToken)
    {
        foreach (var mech in SaslMechanismFactory.GetSupportedMechanisms(Options, Server!))
        {
            if (SaslMechs.Contains(mech))
            {
                SelectedSaslMech = SaslMechanismFactory.CreateMechanism(mech, Options);
                if (SelectedSaslMech.SupportsChannelBinding)
                {
                    var uniqueData = Connection.GetChannelBinding(ChannelBindingKind.Unique);
                    var endpointData = Connection.GetChannelBinding(ChannelBindingKind.Endpoint);

                    if (!SelectedSaslMech.SetChannelBindingData(uniqueData, endpointData))
                    {
                        // we want binding but it's not supported; skip this mech
                        SelectedSaslMech = null;
                        SaslMechs.Remove(mech);
                        continue;
                    }
                }

                SaslMechs.Remove(mech);
                _ = SendAsync(PrepareCommand("AUTHENTICATE", [mech]), cancellationToken);
                return;
            }
        }

        // no more mechs in common
        SelectedSaslMech = null;

        if (SuspendCapEndForSasl)
        {
            SuspendCapEndForSasl = false;
            _ = SendAsync(PrepareCommand("CAP", _END), cancellationToken);
        }
    }

    private void OnSaslCommand(ICommand command, CancellationToken cancellationToken)
    {
        bool done = false;

        if (SelectedSaslMech == null)
        {
            // unexpected AUTHENTICATE command; ignore
            return;
        }

        if (command.Args[0] == "+")
        {
            // have full server response, do whatever we need with it
            done = true;
        }
        else
        {
            SaslBuffer.Append(command.Args[0]);

            // received the last line of data
            if (command.Args[0].Length < 400)
            {
                done = true;
            }

            // prevent DOS from malicious servers by making buffer expand forever
            // if the buffer grows too large, we abort SASL
            if (SaslBuffer.Length > SASL_BUFFER_MAX_LENGTH)
            {
                SaslBuffer.Clear();
                SelectedSaslMech = null;
                _ = SendAsync(PrepareCommand("AUTHENTICATE", _STAR), cancellationToken);
            }
        }

        if (done)
        {
            byte[] data;
            if (SaslBuffer.Length > 0)
            {
                data = Convert.FromBase64String(SaslBuffer.ToString());
                SaslBuffer.Clear();
            }
            else
            {
                data = [];
            }

            bool success = SelectedSaslMech!.Authenticate(data, out var responseBytes);

            if (!success)
            {
                // abort SASL
                SelectedSaslMech = null;
                _ = SendAsync(PrepareCommand("AUTHENTICATE", _STAR), cancellationToken);
            }
            else
            {
                // send response
                if (responseBytes.Length == 0)
                {
                    _ = SendAsync(PrepareCommand("AUTHENTICATE", _PLUS), cancellationToken);
                }
                else
                {
                    var response = Convert.ToBase64String(responseBytes);
                    int start = 0;

                    do
                    {
                        int end = Math.Min(start + 400, response.Length);
                        _ = SendAsync(PrepareCommand("AUTHENTICATE", [response[start..end]]), cancellationToken);
                        start = end;
                    } while (start < response.Length);

                    if (response.Length % 400 == 0)
                    {
                        // if we sent exactly 400 bytes in the last line, send a blank line to let server know we're done
                        _ = SendAsync(PrepareCommand("AUTHENTICATE", _PLUS), cancellationToken);
                    }
                }
            }
        }
    }

    private void AbortUserRegistration(object? sender, NetworkEventArgs e)
    {
        if (e.Exception != null)
        {
            _userRegistrationCompletionSource?.SetException(e.Exception);
        }
        else
        {
            _userRegistrationCompletionSource?.SetCanceled();
        }
    }

    public INetworkInfo AsNetworkInfo() => State;

    public bool TryGetEnabledCap(string cap, out string? value) => State.TryGetEnabledCap(cap, out value);

    public bool TryGetISupport(ISupportToken token, out string? value) => State.TryGetISupport(token, out value);

    public string? GetISupportOrDefault(ISupportToken token, string? defaultValue = null) => State.GetISupportOrDefault(token, defaultValue);
}
