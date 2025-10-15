// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.Extensions.Logging;

using Netwolf.Transport.Commands;
using Netwolf.Transport.Events;
using Netwolf.Transport.Exceptions;
using Netwolf.Transport.Extensions;
using Netwolf.Transport.Internal;
using Netwolf.Transport.RateLimiting;
using Netwolf.Transport.State;

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.RateLimiting;

using static Netwolf.Transport.IRC.INetwork;

namespace Netwolf.Transport.IRC;

/// <summary>
/// A network that we connect to as a client
/// </summary>
public partial class Network : INetwork
{
    private bool _disposed;

    /// <inheritdoc />
    public NetworkOptions Options { get; init; }

    /// <summary>
    /// Logger for this Network
    /// </summary>
    protected ILogger<INetwork> Logger { get; init; }

    protected ICommandFactory CommandFactory { get; init; }

    protected IConnectionFactory ConnectionFactory { get; init; }

    protected IRateLimiter RateLimiter { get; init; }

    protected CommandListenerRegistry CommandListenerRegistry { get; init; }

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

    /// <inheritdoc />
    public ServerRecord? Server { get; set; }

    private IConnection? _connection;

    /// <summary>
    /// A connection to the network
    /// </summary>
    protected internal IConnection Connection
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _connection ?? throw new InvalidOperationException("Network is disconnected.");
        }
    }

    /// <inheritdoc />
    public string Name { get; init; }

    /// <summary>
    /// Network state
    /// </summary>
    private NetworkState State { get; set; }

    /// <inheritdoc />
    public INetworkInfo AsNetworkInfo() => State;

    /// <inheritdoc />
    public bool IsConnected => _connection?.IsConnected == true && _userRegistrationCompletionSource == null;

    #region Deprecated state mutators
    /// <summary>
    /// Limits for this connection.
    /// </summary>
    protected NetworkLimits Limits
    {
        get => State.Limits;
        set => State = State with { Limits = value };
    }

    /// <summary>
    /// Nickname for this connection.
    /// </summary>
    protected string Nick
    {
        get => State.Nick;
        set => State = State with
        {
            Lookup = State.Lookup
                .Remove(IrcUtil.Casefold(State.Nick, CaseMapping))
                .Add(IrcUtil.Casefold(value, CaseMapping), State.ClientId),
            Users = State.Users.SetItem(State.ClientId, State.Users[State.ClientId] with { Nick = value })
        };
    }

    /// <summary>
    /// Hostname for this connection.
    /// </summary>
    protected string Host
    {
        get => State.Host;
        set => State = State with { Users = State.Users.SetItem(State.ClientId, State.Users[State.ClientId] with { Host = value }) };
    }

    /// <summary>
    /// Account name for this connection, or null if not logged in.
    /// </summary>
    protected string? Account
    {
        get => State.Account;
        set => State = State with { Users = State.Users.SetItem(State.ClientId, State.Users[State.ClientId] with { Account = value }) };
    }
    #endregion

    /// <summary>
    /// Case mapping in use.
    /// </summary>
    private CaseMapping CaseMapping { get; set; } = CaseMapping.Ascii;

    /// <summary>
    /// Internal event stream for Command events
    /// </summary>
    private readonly Subject<CommandEventArgs> _commandEventStream = new();

    /// <summary>
    /// Thread to run CommandReceived events on so that (synchronous) command observers are processed in the order they are received
    /// </summary>
    private readonly EventLoopScheduler _commandEventScheduler;

    /// <inheritdoc />
    public IObservable<CommandEventArgs> CommandReceived => _commandEventStream.ObserveOn(_commandEventScheduler);

    /// <inheritdoc />
    public event EventHandler<NetworkEventArgs>? NetworkConnecting;

    /// <inheritdoc />
    public event EventHandler<NetworkEventArgs>? NetworkConnected;

    /// <inheritdoc />
    public event EventHandler<NetworkEventArgs>? NetworkDisconnected;

    /// <summary>
    /// Represents the observable on CommandListenerRegistry for CAP events.
    /// </summary>
    private readonly IDisposable _capEventObserver;

    /// <inheritdoc />
    public event CapFilterEventHandler? CapFilter;

    /// <inheritdoc />
    public async ValueTask<bool> ShouldEnableCapAsync(string capName, string? capValue, string subcommand, CancellationToken cancellationToken)
    {
        if (CapFilter == null)
        {
            return false;
        }

        CapEventArgs args = new(this, capName, capValue, subcommand, cancellationToken);
        foreach (var handler in CapFilter.GetInvocationList().Cast<CapFilterEventHandler>())
        {
            if (await handler(this, args).ConfigureAwait(false))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public event EventHandler<CapEventArgs>? CapEnabled;

    /// <inheritdoc />
    public event EventHandler<CapEventArgs>? CapDisabled;

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
    /// <param name="rateLimiter"></param>
    /// <param name="commandListenerRegistry"></param>
    public Network(
        string name,
        NetworkOptions options,
        ILogger<INetwork> logger,
        ICommandFactory commandFactory,
        IConnectionFactory connectionFactory,
        IRateLimiter rateLimiter,
        CommandListenerRegistry commandListenerRegistry)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(options);

        Name = name;
        Options = options;
        Logger = logger;
        CommandFactory = commandFactory;
        ConnectionFactory = connectionFactory;
        RateLimiter = rateLimiter;
        CommandListenerRegistry = commandListenerRegistry;

        ResetState();

        // define the event loop thread for CommandReceived events
        _commandEventScheduler = new(MakeCommandObserverEventLoop);

        // spin up the message loop for this Network
        _messageLoop = Task.Run(MessageLoop);

        // register command listeners via IObservable
        CommandReceived.SubscribeAsync(OnCommandReceived, _messageLoopTokenSource.Token);
        CommandListenerRegistry.RegisterForNetwork(this, _messageLoopTokenSource.Token);

        // wire up the CAP events
        _capEventObserver = CommandListenerRegistry.CommandListenerEvents
            .OfType<CapEventArgs>()
            .Subscribe(HandleCapEvent);
    }


    #region IDisposable / IAsyncDisposable
    /// <summary>
    /// Perform cleanup of managed resources asynchronously.
    /// </summary>
    /// <returns>Awaitable ValueTask for the async cleanup operation</returns>
    protected virtual async ValueTask DisposeAsyncCore()
    {
        _capEventObserver.Dispose();
        _messageLoopTokenSource.Cancel();
        await _messageLoop.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        _commandEventStream.Dispose();
        _commandEventScheduler.Dispose();
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
                _capEventObserver.Dispose();
                _messageLoopTokenSource.Cancel();
                _messageLoop.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing).GetAwaiter().GetResult();
                _commandEventStream.Dispose();
                _commandEventScheduler.Dispose();
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
    #endregion

    private Thread MakeCommandObserverEventLoop(ThreadStart start)
    {
        return new(() =>
        {
            try
            {
                start.Invoke();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An unhandled exception occurred in a command observer; terminating network connection.");
                DisconnectAsync(null, ex).Wait();
            }
        });
    }

    private void MessageLoop()
    {
        bool activityFlag = false;
        Task<ICommand>? receiveTask = null;

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
                // only fire a new ReceiveAsync if we've fully received the previous one
                receiveTask ??= Connection.ReceiveAsync(token);
                tasks.Add(receiveTask); // index 1
                tasks.Add(pingTimer); // index 2
                tasks.AddRange(pingTimeoutTimers); // index 3+
            }
            else if (_connection?.IsConnected == true)
            {
                // in user registration (not fully connected yet, so do not send any PINGs)
                receiveTask ??= Connection.ReceiveAsync(token);
                tasks.Add(receiveTask); // index 1
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
                    receiveTask = null;
                    if (tasks[index].Status == TaskStatus.Faulted)
                    {
                        // Remote end died, close the connection
                        Logger.LogError(tasks[index].Exception, "ReceiveAsync failed; terminating connection.");
                        goto default;
                    }

                    // Mark the connection as active so we don't send unnecessary PINGs
                    activityFlag = true;
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
                        ProcessServerCommand(command, token);
                    }
                    catch (Exception e)
                    {
                        // if a command callback failed due to an exception, don't crash the message loop
                        Logger.LogError(e, "An error occurred while handling a {Command} from the server", command.Verb);
                    }
                        
                    break;
                case 2:
                    // pingTimer fired, send a PING if necessary and reset the timer
                    pingTimer = Task.Delay(Options.PingInterval, token);
                    if (!activityFlag)
                    {
                        pingTimeoutTimers.Add(Task.Delay(Options.PingTimeout, token));
                        string cookie = String.Format("NWPC{0:X16}", Random.Shared.NextInt64());
                        pingTimeoutCookies.Add(cookie);
                        _ = UnsafeSendRawAsync($"PING {cookie}", token);
                    }

                    // reset activity so that if we make it to another PingInterval with no activity we send a PING
                    activityFlag = false;
                    break;
                default:
                    // one of the pingTimeoutTimers fired or ReceiveAsync threw an exception
                    // this leaves the internal state "dirty" (by not cleaning up pingTimeoutTimers/Cookies, etc.)
                    // but on reconnect the completion source will be flagged and reset those
                    if (_connection != null)
                    {
                        _connection.DisconnectAsync().Wait();
                        _connection = null;

                        AbortUserRegistration(tasks[index].Exception);
                        NetworkDisconnected?.Invoke(this, new(this, tasks[index].Exception));
                    }
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

        if (Options.Servers.Length == 0)
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
                using var connectTimer = new CancellationTokenSource();
                using var connectAggregate = CancellationTokenSource.CreateLinkedTokenSource(connectTimer.Token, cancellationToken);
                _connection = ConnectionFactory.Create(this, server, Options);
                Server = server;

                if (Options.ConnectTimeout != TimeSpan.Zero)
                {
                    connectTimer.CancelAfter(Options.ConnectTimeout);
                }

                try
                {
                    // clean up any old state
                    ResetState();

                    // set up completion source for user registration
                    // this needs to go before we initiate connection so that IsConnected properly returns false
                    _userRegistrationCompletionSource = new TaskCompletionSource();

                    // attempt the connection
                    Logger.LogInformation("Connecting to {server}...", server);
                    await _connection.ConnectAsync(connectAggregate.Token).ConfigureAwait(false);
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
                    _userRegistrationCompletionSource!.SetCanceled(connectAggregate.Token);
                    await _connection.DisposeAsync().ConfigureAwait(false);
                    continue;
                }

                // Successfully connected, do user registration
                // don't exit the loop until registration succeeds
                // (we want to bounce/reconnect if that fails for whatever reason)
                Logger.LogInformation("Connected to {server}.", server);
                NetworkConnecting?.Invoke(this, new(this));

                // alert the message loop to start processing incoming commands
                _messageLoopCompletionSource.SetResult();

                // default to true so that if we abort prematurely, we skip to the error message
                // about the connection being aborted rather user registration timing out
                bool registrationComplete = true;
                var commandOptions = CommandCreationOptions.MakeOptions(State);

                // re-using the previous CTS is too risky as the timer could have fired between the successful connection and now
                // so TryReset or re-applying CancelAfter is not guaranteed to work. Best to make a new CTS.
                using var registrationTimer = new CancellationTokenSource();
                using var registrationAggregate = CancellationTokenSource.CreateLinkedTokenSource(registrationTimer.Token, cancellationToken);

                if (Options.RegistrationTimeout != TimeSpan.Zero)
                {
                    registrationTimer.CancelAfter(Options.RegistrationTimeout);
                }

                try
                {
                    await UnsafeSendRawAsync("CAP LS 302", registrationAggregate.Token);

                    if (Options.ServerPassword != null)
                    {
                        await Connection.SendAsync(
                            CommandFactory.PrepareClientCommand(State.Self, "PASS", [Options.ServerPassword], null, commandOptions),
                            registrationAggregate.Token);
                    }

                    await Connection.SendAsync(
                        CommandFactory.PrepareClientCommand(State.Self, "NICK", [Options.PrimaryNick], null, commandOptions),
                        registrationAggregate.Token);

                    // Most networks outright ignore params 2 and 3.
                    // Some follow RFC 2812 and treat param 2 as a bitfield where 4 = +w and 8 = +i.
                    // Others may allow an arbitrary user mode string in param 2 prefixed with +.
                    // For widest compatibility, leave both unspecified and just handle umodes post-registration.
                    await Connection.SendAsync(
                        CommandFactory.PrepareClientCommand(State.Self, "USER", [Options.Ident, "0", "*", Options.RealName], null, commandOptions),
                        registrationAggregate.Token);

                    // Handle any responses to the above. Notably, we might need to choose a new nickname,
                    // handle CAP negotiation (and SASL), or be disconnected outright. This will block the
                    // ConnectAsync method until registration is fully completed (whether successfully or not).
                    await _userRegistrationCompletionSource.Task.WaitAsync(registrationAggregate.Token);
                }
                catch (OperationCanceledException)
                {
                    // allow other exceptions (e.g. from PrepareCommand) to bubble up and abort entirely,
                    // as those errors are not recoverable and will fail again if tried again

                    // If the timeout was reached in the final await, this will be false (which indicates registration timeout)
                    // otherwise, it will be true to indicate the user requested that the connection attempt be aborted
                    // and we want to log a different message in that case
                    registrationComplete = cancellationToken.IsCancellationRequested;
                }

                if (_userRegistrationCompletionSource.Task.IsCompletedSuccessfully)
                {
                    // user registration succeeded, exit out of the connect loop
                    _userRegistrationCompletionSource = null;
                    NetworkConnected?.Invoke(this, new(this));
                    return;
                }
                else if (!registrationComplete)
                {
                    Logger.LogInformation("User registration timed out, trying next server in list.");
                    await _connection.DisposeAsync().ConfigureAwait(false);
                }
                else
                {
                    Logger.LogInformation("Connection aborted.");
                    await _connection.DisposeAsync().ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
        }

        // Getting here means we ran out of connect retries
        Logger.LogError("Unable to connect to network, maximum retries reached.");
        throw new ConnectionException("Unable to connect to network, maximum retries reached.");
    }

    /// <inheritdoc />
    public Task DisconnectAsync(string? reason = null)
    {
        return DisconnectAsync(reason, null);
    }

    /// <summary>
    /// Disconnects from the network, potentially with an exception state
    /// </summary>
    /// <param name="reason"></param>
    /// <param name="ex"></param>
    /// <returns></returns>
    protected async Task DisconnectAsync(string? reason, Exception? ex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            await Connection.SendAsync(
                CommandFactory.PrepareClientCommand(State.Self, "QUIT", [reason], null, CommandCreationOptions.MakeOptions(State)),
                default).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // swallow exceptions here
        }

        if (_connection != null)
        {
            await _connection.DisconnectAsync().ConfigureAwait(false);
        }

        _connection = null;
        Server = null;
        _messageLoopCompletionSource.SetResult();
        AbortUserRegistration(ex);
        NetworkDisconnected?.Invoke(this, new(this, ex));
    }

    /// <summary>
    /// Send a rate-limited command to the network
    /// </summary>
    /// <param name="command">Command to send</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns></returns>
    /// <exception cref="RateLimitLeaseAcquisitionException">If we're unable to acquire a rate limit lease (e.g. queue is full)</exception>
    protected async Task InternalSendAsync(ICommand command, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        using var commandLease = await RateLimiter.AcquireAsync(command, cancellationToken).ConfigureAwait(false);
        if (!commandLease.IsAcquired)
        {
            commandLease.TryGetMetadata(MetadataName.ReasonPhrase, out var reason);
            Logger.LogError("Unable to send {Command}: {Reason}", command.Verb, reason ?? "Unknown error.");
            throw new RateLimitLeaseAcquisitionException(command, commandLease.GetAllMetadata().ToDictionary());
        }

        await Connection.SendAsync(command, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public DeferredCommand SendAsync(ICommand command, CancellationToken cancellationToken = default)
    {
        return new(InternalSendAsync, CommandReceived.Select(c => c.Command), command, cancellationToken);
    }

    /// <inheritdoc />
    public DeferredCommand SendRawAsync(string rawLine, CancellationToken cancellationToken = default)
    {
        var parsed = CommandFactory.Parse(CommandType.Client, rawLine);
        var command = CommandFactory.PrepareClientCommand(State.Self, parsed.Verb, parsed.Args, parsed.Tags, CommandCreationOptions.MakeOptions(State));
        return new(InternalSendAsync, CommandReceived.Select(c => c.Command), command, cancellationToken);
    }

    /// <inheritdoc />
    public Task UnsafeSendRawAsync(string rawLine, CancellationToken cancellationToken)
    {
        return Connection.UnsafeSendRawAsync(rawLine, cancellationToken);
    }

    #region Command handling
    private async Task OnCommandReceived(CommandEventArgs args)
    {
        var command = args.Command;
        var cancellationToken = args.Token;

        cancellationToken.ThrowIfCancellationRequested();

        // use INetworkInfo rather than NetworkState directly to gain access to default interface implementations
        INetworkInfo info = AsNetworkInfo();

        if (!IsConnected)
        {
            // callbacks we only handle if we're pre-registration
            // TODO: make use of DeferredCommand here to keep logic together
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
                        await UnsafeSendRawAsync($"NICK {secondary}", cancellationToken);
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
                    await UnsafeSendRawAsync($"WHO {State.Nick}", cancellationToken);
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
                        await UnsafeSendRawAsync("CAP END", cancellationToken);
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
                List<ISupportToken> removedISupport = [];
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

                    // tokens can begin with '-' to indicate they are being negated
                    if (token[0] == '-')
                    {
                        removedISupport.Add(new(token[1..]));
                        continue;
                    }

                    // not being negated, it's a new token
                    newISupport[new(token)] = value;

                    // we process some tokens here too because we need the info
                    switch (token)
                    {
                        case "CASEMAPPING":
                            {
                                var old = CaseMapping;

                                CaseMapping = value switch
                                {
                                    "ascii" => CaseMapping.Ascii,
                                    "rfc1459" => CaseMapping.Rfc1459,
                                    "rfc1459-strict" => CaseMapping.Rfc1459Strict,
                                    _ => CaseMapping.Ascii
                                };

                                if (value != "ascii" && CaseMapping == CaseMapping.Ascii)
                                {
                                    Logger.LogWarning("Received unsupported CASEMAPPING token {CaseMapping}; defaulting to ascii", value);
                                }

                                if (CaseMapping != old)
                                {
                                    // need to remap our lookup table
                                    State = State with
                                    {
                                        CaseMapping = CaseMapping,
                                        Lookup = State.Lookup.ToImmutableDictionary(
                                            x => IrcUtil.Casefold(
                                                State.Users.GetValueOrDefault(x.Value)?.Nick
                                                    ?? State.Channels.GetValueOrDefault(x.Value)?.Name
                                                    ?? throw new BadStateException("Lookup map contains an unrecognized user or channel"),
                                                CaseMapping),
                                            x => x.Value)
                                    };
                                }
                            }

                            break;
                        case "LINELEN":
                            if (int.TryParse(value, out int lineLen))
                            {
                                Limits = Limits with { LineLength = Math.Max(lineLen, Limits.LineLength) };
                            }
                            break;
                    }
                }

                State = State with { ISupport = State.ISupport.SetItems(newISupport).RemoveRange(removedISupport) };
                break;
        }
    }
    #endregion

    /// <summary>
    /// Abort user registration if it is currently underway.
    /// If user registration has already completed or has already been aborted, this call is a no-op.
    /// </summary>
    /// <param name="ex"></paaaram>
    private void AbortUserRegistration(Exception? ex)
    {
        if (_userRegistrationCompletionSource == null || _userRegistrationCompletionSource.Task.IsCompleted)
        {
            return;
        }
        else if (ex != null)
        {
            _userRegistrationCompletionSource.SetException(ex);
        }
        else
        {
            _userRegistrationCompletionSource.SetCanceled();
        }
    }

    private void HandleCapEvent(CapEventArgs args)
    {
        switch (args.Subcommand)
        {
            case "LS":
            case "NEW":
                State = State with { SupportedCaps = State.SupportedCaps.SetItem(args.CapName, args.CapValue) };
                break;
            case "ACK":
            case "LIST":
                State = State with
                {
                    EnabledCaps = State.EnabledCaps.Add(args.CapName),
                    // the ircd might let us enable a cap it doesn't explicitly advertise...
                    SupportedCaps = State.SupportedCaps.ContainsKey(args.CapName)
                        ? State.SupportedCaps
                        : State.SupportedCaps.Add(args.CapName, null)
                };

                CapEnabled?.Invoke(this, args);
                break;
            case "DEL":
                State = State with
                {
                    SupportedCaps = State.SupportedCaps.Remove(args.CapName),
                    EnabledCaps = State.EnabledCaps.Remove(args.CapName),
                };

                CapDisabled?.Invoke(this, args);
                break;
        }
    }

    [SuppressMessage("Style", "IDE0305:Simplify collection initialization", Justification = "ToList() is more semantically meaningful")]
    private void RemoveUserFromChannel(UserRecord user, ChannelRecord channel)
    {
        // is this us?
        if (user.Id == State.ClientId)
        {
            // if we left a channel, remove the channel from all users and clear our lookup entry
            Logger.LogTrace("Cleaning up channel {Channel} because we left it", channel.Name);

            List<string> lookupRemove = [IrcUtil.Casefold(channel.Name, CaseMapping)];
            List<UserRecord> userRemove = State.GetAllUsers()
                .Where(u => u.Channels.Count == 1 && u.Channels.ContainsKey(channel.Id))
                .ToList();

            lookupRemove.AddRange(userRemove.Select(u => IrcUtil.Casefold(u.Nick, CaseMapping)));
            userRemove.ForEach(u => Logger.LogTrace("Cleaning up user {Nick} because we left {Channel} and share no other channels with them", u.Nick, channel.Name));

            State = State with
            {
                Lookup = State.Lookup.RemoveRange(lookupRemove),
                Channels = State.Channels.Remove(channel.Id),
                Users = State.Users
                    .RemoveRange(userRemove.Select(u => u.Id))
                    .ToImmutableDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value with { Channels = kvp.Value.Channels.Remove(channel.Id) }
                    ),
            };
        }
        else
        {
            // someone else left a channel, just need to update their record
            // don't re-add the channel or user if it was already removed from state
            if (State.Users.ContainsKey(user.Id))
            {
                State = State with
                {
                    Users = State.Users.SetItem(user.Id, user with { Channels = user.Channels.Remove(channel.Id) }),
                };
            }

            if (State.Channels.ContainsKey(channel.Id))
            {
                State = State with
                {
                    Channels = State.Channels.SetItem(channel.Id, channel with { Users = channel.Users.Remove(user.Id) }),
                };
            }

            // this might not be necessary anymore after commands are moved out and use UnsafeUpdateUser and UnsafeUpdateChannel
            if (user.Channels.Count == 1 && user.Channels.ContainsKey(channel.Id))
            {
                Logger.LogTrace("Cleaning up user {Nick} because they left {Channel} and share no other channels with us", user.Nick, channel.Name);
                State = State with
                {
                    Lookup = State.Lookup.Remove(IrcUtil.Casefold(user.Nick, CaseMapping)),
                    Users = State.Users.Remove(user.Id),
                };
            }
        }
    }

    /// <summary>
    /// Mark the network as connected and fully registered without utilizing the underlying Connection.
    /// This method is intended for unit tests where we can avoid having a full IRC protocol registration,
    /// therefore making tests complete much more quickly and be narrowly tailored to the items under test.
    /// The message loop is also terminated when this is called.
    /// </summary>
    /// <param name="server"></param>
    /// <param name="host"></param>
    /// <param name="account"></param>
    internal void RegisterForUnitTests(string host, string? account = null)
    {
        _messageLoopTokenSource.Cancel();
        
        // re-add command handlers since the above cancellation removed them
        // The GC will need to collect these subscriptions at the end but we're unit testing so that's probably fine ;)
        CommandReceived.SubscribeAsync(OnCommandReceived, default);
        CommandListenerRegistry.RegisterForNetwork(this, default);

        // This is expected to be a mock/stub connection via DI service replacement in the test harness
        _connection = ConnectionFactory.Create(this, Options.Servers[0], Options);
        Host = host;
        Account = account;
    }

    /// <summary>
    /// Receive a raw protocol line, for use in unit testing.
    /// </summary>
    /// <param name="line"></param>
    /// <returns>A Task that can be awaited to ensure it is fully processed before continuing with test execution</returns>
    internal async Task ReceiveLineForUnitTests(string line)
    {
        TaskCompletionSource completionSource = new();

        void OnDisconnected(object? sender, NetworkEventArgs args)
        {
            if (args.Network == this)
            {
                if (args.Exception is Exception ex)
                {
                    completionSource.SetException(ex);
                }
                else
                {
                    completionSource.SetCanceled();
                }
            }
        }

        var subscription = CommandReceived.Subscribe(args => completionSource.SetResult());
        NetworkDisconnected += OnDisconnected;

        try
        {
            if (ProcessServerCommand(CommandFactory.Parse(CommandType.Server, line), default))
            {
                await completionSource.Task;
            }
        }
        finally
        {
            NetworkDisconnected -= OnDisconnected;
            subscription.Dispose();
        }
    }

    [MemberNotNull(nameof(State))]
    [SuppressMessage("Style", "IDE0301:Simplify collection initialization",
        Justification = "Immutable*.Empty is more indicative of what the data type is and requires no allocations")]
    private void ResetState()
    {
        Guid clientId = Guid.NewGuid();
        UserRecord client = new(
            clientId,
            Options.PrimaryNick,
            Options.Ident,
            string.Empty,
            null,
            false,
            Options.RealName,
            ImmutableHashSet<char>.Empty,
            ImmutableDictionary<Guid, string>.Empty);

        State = new(
            Name,
            new(),
            clientId,
            CaseMapping,
            ImmutableDictionary.CreateRange<Guid, UserRecord>([new(clientId, client)]),
            ImmutableDictionary<Guid, ChannelRecord>.Empty,
            ImmutableDictionary.CreateRange<string, Guid>([new(IrcUtil.Casefold(Options.PrimaryNick, CaseMapping), clientId)]),
            ImmutableDictionary<string, string?>.Empty,
            ImmutableHashSet<string>.Empty,
            ImmutableDictionary<ISupportToken, string?>.Empty);
    }

    /// <inheritdoc />
    public void UnsafeReceiveRaw(string command, CancellationToken cancellationToken)
    {
        ProcessServerCommand(CommandFactory.Parse(CommandType.Server, command), cancellationToken);
    }

    private bool ProcessServerCommand(ICommand command, CancellationToken cancellationToken)
    {
        // ignore known commands with incorrect arity
        cancellationToken.ThrowIfCancellationRequested();
        if (ArityHelper.CheckArity(State, command.Verb, command.Args.Count))
        {
            _commandEventStream.OnNext(new(this, command, cancellationToken));
            return true;
        }
        else
        {
            Logger.LogWarning("Protocol violation: receieved {Command} with incorrect arity. Message will be ignored.", command.Verb);
            return false;
        }
    }

    /// <inheritdoc />
    public void UnsafeUpdateUser(UserRecord user)
    {
        if (State.Users.TryGetValue(user.Id, out var existing))
        {
            if (user.Id != State.ClientId && user.Channels.Count == 0)
            {
                Logger.LogTrace("Cleaning up user {Nick} because they left all shared channels", existing.Nick);

                State = State with
                {
                    Lookup = State.Lookup.Remove(IrcUtil.Casefold(existing.Nick, CaseMapping)),
                    Users = State.Users.Remove(user.Id),
                };
            }
            else
            {
                State = State with
                {
                    Lookup = State.Lookup
                        .Remove(IrcUtil.Casefold(existing.Nick, CaseMapping))
                        .Add(IrcUtil.Casefold(user.Nick, CaseMapping), user.Id),
                    Channels = State.Channels
                        .SetItems(user.Channels.Keys.Where(State.Channels.ContainsKey).Select(c =>
                            new KeyValuePair<Guid, ChannelRecord>(
                                c,
                                State.Channels[c] with { Users = State.Channels[c].Users.SetItem(user.Id, user.Channels[c]) }))),
                    Users = State.Users.SetItem(user.Id, user)
                };
            }

            var removedFromChannels = from channel in State.Channels.Values
                                      where channel.Users.ContainsKey(user.Id) && !user.Channels.ContainsKey(channel.Id)
                                      select channel;

            foreach (var channel in removedFromChannels)
            {
                RemoveUserFromChannel(user, channel);
            }
            
        }
        else if (user.Id == State.ClientId || user.Channels.Count > 0)
        {
            State = State with
            {
                Lookup = State.Lookup.Add(IrcUtil.Casefold(user.Nick, CaseMapping), user.Id),
                Channels = State.Channels
                    .SetItems(user.Channels.Keys.Where(State.Channels.ContainsKey).Select(c =>
                        new KeyValuePair<Guid, ChannelRecord>(
                            c,
                            State.Channels[c] with { Users = State.Channels[c].Users.SetItem(user.Id, user.Channels[c]) }))),
                Users = State.Users.SetItem(user.Id, user)
            };
        }
    }

    /// <inheritdoc />
    public void UnsafeUpdateChannel(ChannelRecord channel)
    {
        if (State.Channels.TryGetValue(channel.Id, out var existing))
        {
            if (channel.Users.Count == 0)
            {
                State = State with
                {
                    Lookup = State.Lookup.Remove(IrcUtil.Casefold(existing.Name, CaseMapping)),
                    Channels = State.Channels.Remove(existing.Id),
                };
            }
            else
            {
                State = State with
                {
                    Lookup = State.Lookup
                        .Remove(IrcUtil.Casefold(existing.Name, CaseMapping))
                        .Add(IrcUtil.Casefold(channel.Name, CaseMapping), channel.Id),
                    Channels = State.Channels.SetItem(channel.Id, channel),
                    Users = State.Users
                        .SetItems(channel.Users.Keys.Where(State.Users.ContainsKey).Select(u =>
                            new KeyValuePair<Guid, UserRecord>(
                                u,
                                State.Users[u] with { Channels = State.Users[u].Channels.SetItem(channel.Id, channel.Users[u]) })))
                };
            }

            var removedUsers = from userId in existing.Users.Keys
                               where !channel.Users.ContainsKey(userId)
                               select State.Users[userId];

            foreach (var user in removedUsers)
            {
                RemoveUserFromChannel(user, channel);
            }
        }
        else if (channel.Users.Count > 0)
        {
            State = State with
            {
                Lookup = State.Lookup.Add(IrcUtil.Casefold(channel.Name, CaseMapping), channel.Id),
                Channels = State.Channels.SetItem(channel.Id, channel),
                Users = State.Users
                    .SetItems(channel.Users.Keys.Where(State.Users.ContainsKey).Select(u =>
                        new KeyValuePair<Guid, UserRecord>(
                            u,
                            State.Users[u] with { Channels = State.Users[u].Channels.SetItem(channel.Id, channel.Users[u]) })))
            };
        }
    }
}
