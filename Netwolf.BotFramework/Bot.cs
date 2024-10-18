using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Netwolf.Attributes;
using Netwolf.BotFramework.Exceptions;
using Netwolf.BotFramework.Internal;
using Netwolf.BotFramework.Services;
using Netwolf.PluginFramework.Commands;
using Netwolf.Transport.Events;
using Netwolf.Transport.IRC;

using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

namespace Netwolf.BotFramework;

/// <summary>
/// Base class for bots created using this framework.
/// Your code should create a subclass of Bot with your desired functionality (commands, etc.).
/// Attributes are used to decorate methods to describe how it interacts with the bots,
/// e.g. the CommandAttribute is used to define commands the bot responds to from IRC.
/// </summary>
public abstract class Bot : IDisposable, IAsyncDisposable
{
    private bool _disposed = false;

    /// <summary>
    /// Name of the bot
    /// </summary>
    public string BotName { get; private init; }

    /// <summary>
    /// Logger used to log events
    /// </summary>
    protected ILogger<Bot> Logger { get; private init; }

    private IOptionsMonitor<BotOptions> OptionsMonitor { get; init; }

    /// <summary>
    /// A snapshot of the bot options as defined by configuration
    /// </summary>
    protected BotOptions Options => OptionsMonitor.Get(BotName);

    /// <summary>
    /// The network the bot is connected to.
    /// </summary>
    private INetwork Network { get; init; }

    /// <summary>
    /// Internal marker for when bot initialization completes
    /// (i.e. connected to IRC and joined all configured channels).
    /// User commands are not executed until initialization completes.
    /// </summary>
    private bool Initialized { get; set; } = false;

    /// <summary>
    /// TCS used to keep the main execution Task suspended until the bot disconnects
    /// </summary>
    private TaskCompletionSource DisconnectionSource { get; init; }

    /// <summary>
    /// Subscription to the underlying <see cref="Network"/>'s command event stream
    /// </summary>
    private IDisposable CommandSubscription { get; set; }

    /// <summary>
    /// Allows for cancelling all outstanding Tasks when <see cref="DisconnectAsync(string)"/> is called.
    /// </summary>
    private CancellationTokenSource? CancellationSource { get; set; }

    private ICommandDispatcher<BotCommandResult> CommandDispatcher { get; init; }

    private ICommandFactory CommandFactory { get; init; }

    private BotCommandContextFactory BotCommandContextFactory { get; init; }

    private IEnumerable<ICapProvider> CapProviders { get; init; }

    private ValidationContextFactory ValidationContextFactory { get; init; }

    private PartitionedRateLimiter<ICommand> CommandRateLimiter { get; init; }

    private PartitionedRateLimiter<ICommand> ByteRateLimiter { get; init; }

    internal IObservable<CommandEventArgs> CommandStream => Network.CommandReceived;

    /// <summary>
    /// Constructor; subclasses defining their own constructors must call this one. If the bot is activated
    /// automatically on startup, the BotCreationData parameter will be injected automatically and must be present in
    /// any subclass constructors. If the bot is created manually, the BotCreationData must be injected via FromKeyedServiceAttribute,
    /// with the bot's name as the service key.
    /// </summary>
    /// <param name="data">PDO encapsulating bot services to improve future compatibility with subclass constructors</param>
    public Bot(BotCreationData data)
    {
        BotName = data.BotName;
        Logger = data.Logger;
        OptionsMonitor = data.OptionsMonitor;
        Network = data.NetworkFactory.Create(BotName, Options);
        CommandDispatcher = data.CommandDispatcher;
        CommandFactory = data.CommandFactory;
        BotCommandContextFactory = data.BotCommandContextFactory;
        ValidationContextFactory = data.ValidationContextFactory;
        CapProviders = data.CapProviders;
        DisconnectionSource = new();

        // Initialize rate limiters with a cached options so it can't change on us
        var cachedOptions = Options;
        List<PartitionedRateLimiter<ICommand>> limiters = [];

        // Per-target limiters (PRIVMSG/NOTICE/TAGMSG)
        if (cachedOptions.DefaultPerTargetLimiter.Enabled || cachedOptions.PerTargetLimiter.Any(o => o.Value.Enabled))
        {
            limiters.Add(PartitionedRateLimiter.Create<ICommand, string>(cmd =>
            {
                var target = cmd.GetMessageTarget();
                if (target == null)
                {
                    return RateLimitPartition.GetNoLimiter(string.Empty);
                }

                var config = cachedOptions.PerTargetLimiter.GetValueOrDefault(target, cachedOptions.DefaultPerTargetLimiter);
                if (!config.Enabled)
                {
                    return RateLimitPartition.GetNoLimiter(string.Empty);
                }

                return RateLimitPartition.GetTokenBucketLimiter(target, _ => new()
                {
                    AutoReplenishment = false,
                    QueueLimit = cachedOptions.RateLimiterMaxCommands,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    ReplenishmentPeriod = TimeSpan.FromMilliseconds(config.ReplenishmentRate),
                    TokensPerPeriod = config.ReplenismentAmount,
                    TokenLimit = config.MaxTokens,
                });
            }));
        }

        // Per-command limiters
        if (cachedOptions.PerCommandLimiter.Any(o => o.Value.Enabled))
        {
            limiters.Add(PartitionedRateLimiter.Create<ICommand, string>(cmd =>
            {
                string? key = null;
                string withArity = $"{cmd.Verb}`{cmd.Args.Count}";

                if (cachedOptions.PerCommandLimiter.TryGetValue(withArity, out var config))
                {
                    key = withArity;
                }
                else if (cachedOptions.PerCommandLimiter.TryGetValue(cmd.Verb, out config))
                {
                    key = cmd.Verb;
                }


                if (key == null || !(config?.Enabled ?? false))
                {
                    return RateLimitPartition.GetNoLimiter(string.Empty);
                }

                return RateLimitPartition.GetSlidingWindowLimiter(key, _ => new()
                {
                    AutoReplenishment = false,
                    QueueLimit = cachedOptions.RateLimiterMaxCommands,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    Window = TimeSpan.FromMilliseconds(config.Duration),
                    PermitLimit = config.Limit,
                    SegmentsPerWindow = config.Segments,
                });
            }));
        }

        // Global command limiter
        if (cachedOptions.GlobalCommandLimiter.Enabled)
        {
            limiters.Add(PartitionedRateLimiter.Create<ICommand, string>(_ =>
            {
                return RateLimitPartition.GetTokenBucketLimiter(string.Empty, _ => new()
                {
                    AutoReplenishment = false,
                    QueueLimit = cachedOptions.RateLimiterMaxCommands,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    ReplenishmentPeriod = TimeSpan.FromMilliseconds(cachedOptions.GlobalCommandLimiter.ReplenishmentRate),
                    TokensPerPeriod = cachedOptions.GlobalCommandLimiter.ReplenismentAmount,
                    TokenLimit = cachedOptions.GlobalCommandLimiter.MaxTokens,
                });
            }));
        }

        // If no limiters are enabled, present a dummy one as CreateChained() requires the collection to have at least 1 element
        if (limiters.Count == 0)
        {
            limiters.Add(PartitionedRateLimiter.Create<ICommand, string>(_ =>
            {
                return RateLimitPartition.GetNoLimiter(string.Empty);
            }));
        }

        CommandRateLimiter = PartitionedRateLimiter.CreateChained([.. limiters]);

        // Global bytes limiter
        if (cachedOptions.GlobalByteLimiter.Enabled)
        {
            ByteRateLimiter = PartitionedRateLimiter.Create<ICommand, string>(_ =>
            {
                return RateLimitPartition.GetSlidingWindowLimiter(string.Empty, _ => new()
                {
                    AutoReplenishment = false,
                    QueueLimit = cachedOptions.RateLimiterMaxBytes,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    Window = TimeSpan.FromMilliseconds(cachedOptions.GlobalByteLimiter.Duration),
                    PermitLimit = cachedOptions.GlobalByteLimiter.Limit,
                    SegmentsPerWindow = cachedOptions.GlobalByteLimiter.Segments,
                });
            });
        }
        else
        {
            ByteRateLimiter = PartitionedRateLimiter.Create<ICommand, string>(_ =>
            {
                return RateLimitPartition.GetNoLimiter(string.Empty);
            });
        }

        // Register our network events
        Network.ShouldEnableCap += ShouldEnableCap;
        CommandSubscription = Network.CommandReceived.Subscribe(e => OnCommandReceived(e));
        Network.Disconnected += OnDisconnected;

        // Wire up bot commands
        // There is a reflection-based slow path and a source generator fast path; detect which was used
        var generatedTypes = GetType()
            .Assembly
            .GetCustomAttributes<SourceGeneratedCommandAttribute>()
            .Where(attr => attr.TargetType == GetType())
            .ToList();

        if (generatedTypes.Count > 0 && data.EnableCommandOptimization)
        {
            // fast path
            generatedTypes.ForEach(attr => CommandDispatcher.AddCommand(attr.GeneratedType));
        }
        else
        {
            // slow path
            var commands = GetType()
                .GetMethods()
                .Select(method => (method, attr: method.GetCustomAttribute<CommandAttribute>()))
                .Where(o => o.attr != null)
                .Select(o => new BotCommandThunk(this, o.method, o.attr!, ValidationContextFactory));

            foreach (var command in commands)
            {
                CommandDispatcher.AddCommand(command);
            }
        }
    }

    /// <summary>
    /// Send a raw line to the network, with no validation and bypassing all rate limiters.
    /// This is NOT SAFE to use on user input and misuse may lead to security vulnerabilities.
    /// A CRLF is automatically appended to the end of the line, however lines with embedded CRLF
    /// are allowed and will be interpreted by the remote ircd as multiple commands.
    /// </summary>
    /// <param name="rawLine">A line that conforms to the IRC protocol. No validation or processing is performed.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns></returns>
    public Task UnsafeSendRawAsync(string rawLine, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return Network.UnsafeSendRawAsync(rawLine, cancellationToken);
    }

    /// <summary>
    /// Sends a raw line to the network. The line will be parsed and validated and does not need to have a source defined.
    /// The verb, arguments, and tags from the raw line will be sent.
    /// Rate limiters will apply to the sent command.
    /// A CRLF will be automatically appended to the line and must not be present in the passed-in line.
    /// In general, prefer to use the SendAsync overloads as they are more performant due to not requiring command parsing.
    /// </summary>
    /// <param name="rawLine">A line containing the verb, arguments, and message tags to send.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns></returns>
    public DeferredCommand SendRawAsync(string rawLine, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        var parsed = CommandFactory.Parse(CommandType.Client, rawLine);
        var command = Network.PrepareCommand(parsed.Verb, parsed.Args, parsed.Tags);
        return new(this, command, cancellationToken);
    }

    /// <summary>
    /// Sends a command to the IRC network with the specified verb and arguments.
    /// Rate limiters will apply to the sent command.
    /// </summary>
    /// <param name="verb"></param>
    /// <param name="args"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public DeferredCommand SendAsync(string verb, IEnumerable<object?> args, CancellationToken cancellationToken)
    {
        return SendAsync(verb, args, ImmutableDictionary<string, string?>.Empty, cancellationToken);
    }

    /// <summary>
    /// Sends a command to the IRC network with the specified verb, arguments, and tags.
    /// Rate limiters will apply to the sent command.
    /// </summary>
    /// <param name="verb"></param>
    /// <param name="args"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public DeferredCommand SendAsync(
        string verb,
        IEnumerable<object?> args,
        IReadOnlyDictionary<string, string?> tags,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        var command = Network.PrepareCommand(verb, args, tags);
        return new(this, command, cancellationToken);
    }

    /// <summary>
    /// Sends a message to the given target (user or channel).
    /// Long messages or messages with embedded newlines will be automatically split into multiple lines.
    /// </summary>
    /// <param name="target">Message target</param>
    /// <param name="message">Message to send</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task SendMessageAsync(string target, string message, CancellationToken cancellationToken)
    {
        return SendMessageAsync(target, message, ImmutableDictionary<string, string?>.Empty, cancellationToken);
    }

    /// <summary>
    /// Sends a message to the given target (user or channel) with the specified tags.
    /// Long messages or messages with embedded newlines will be automatically split into multiple lines.
    /// </summary>
    /// <param name="target">Message target</param>
    /// <param name="message">Message to send</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task SendMessageAsync(string target, string message, IReadOnlyDictionary<string, string?> tags, CancellationToken cancellationToken)
    {
        foreach (var command in Network.PrepareMessage(MessageType.Message, target, message, tags))
        {
            await InternalSendAsync(command, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Sends a notice to the given target (user or channel).
    /// Long messages or messages with embedded newlines will be automatically split into multiple lines.
    /// </summary>
    /// <param name="target">Message target</param>
    /// <param name="message">Message to send</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task SendNoticeAsync(string target, string message, CancellationToken cancellationToken)
    {
        return SendNoticeAsync(target, message, ImmutableDictionary<string, string?>.Empty, cancellationToken);
    }

    /// <summary>
    /// Sends a notice to the given target (user or channel) with the specified tags.
    /// Long messages or messages with embedded newlines will be automatically split into multiple lines.
    /// </summary>
    /// <param name="target">Message target</param>
    /// <param name="message">Message to send</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task SendNoticeAsync(string target, string message, IReadOnlyDictionary<string, string?> tags, CancellationToken cancellationToken)
    {
        foreach (var command in Network.PrepareMessage(MessageType.Notice, target, message, tags))
        {
            await InternalSendAsync(command, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Send a rate-limited command to the network
    /// </summary>
    /// <param name="command">Command to send</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns></returns>
    /// <exception cref="RateLimitLeaseAcquisitionException">If we're unable to acquire a rate limit lease (e.g. queue is full)</exception>
    internal async Task InternalSendAsync(ICommand command, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        using var commandLease = await CommandRateLimiter.AcquireAsync(command, 1, cancellationToken).ConfigureAwait(false);
        if (!commandLease.IsAcquired)
        {
            commandLease.TryGetMetadata(MetadataName.ReasonPhrase, out var reason);
            Logger.LogError("Unable to send {Command}: {Reason}", command.Verb, reason ?? "Unknown error.");
            throw new RateLimitLeaseAcquisitionException(command, commandLease.GetAllMetadata().ToDictionary());
        }

        // command.FullCommand omits the trailing CRLF, so add another 2 bytes to account for it
        var byteCount = Encoding.UTF8.GetByteCount(command.FullCommand) + 2;
        using var byteLease = await ByteRateLimiter.AcquireAsync(command, byteCount, cancellationToken).ConfigureAwait(false);
        if (!byteLease.IsAcquired)
        {
            byteLease.TryGetMetadata(MetadataName.ReasonPhrase, out var reason);
            Logger.LogError("Unable to send {Command}: {Reason}", command.Verb, reason ?? "Unknown error.");
            throw new RateLimitLeaseAcquisitionException(command, byteLease.GetAllMetadata().ToDictionary());
        }

        await Network.SendAsync(command, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Disconnect from the network. This task cannot be cancelled.
    /// </summary>
    /// <param name="reason">QUIT reason</param>
    /// <returns></returns>
    public async Task DisconnectAsync(string reason)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // cancel all pending operations
        CancellationSource?.Cancel();

        // send a QUIT
        await Network.DisconnectAsync(reason);
    }

    public Task JoinChannelAsync(string name, CancellationToken cancellationToken)
    {
        return JoinChannelAsync(name, null, cancellationToken);
    }

    static readonly string[] _joinErrors1 = ["403", "405", "471", "473", "474", "475"];
    static readonly string[] _joinErrors0 = ["476"];

    public async Task JoinChannelAsync(string name, string? key, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!Network.IsConnected)
        {
            throw new InvalidOperationException("Bot is not connected to the network.");
        }

        // TODO: validate that the channel name starts with a channel prefix character
        // (and have a different API for /join 0 -- aka part all channels)

        // it's (mostly) safe to continue to use linkedToken after linkedTokenSource is disposed
        // linkedToken.WaitHandle will throw an exception but simply checking cancellation state is safe
        // CancellationSource is guaranteed non-null here since ExecuteAsync() populates it and that was guaranteed to run first in order to get this far
        using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, CancellationSource!.Token);
        var linkedToken = linkedTokenSource.Token;

        var result = await SendAsync("JOIN", [name, key], linkedToken).WithReply(c => c.Verb switch
        {
            var x when _joinErrors0.Contains(x) => c.Args[0] == name,
            var x when _joinErrors1.Contains(x) => c.Args[1] == name,
            "JOIN" => c.Args[0] == name && c.Source == Network.Nick,
            _ => false
        }).ConfigureAwait(false);

        if (result.Verb != "JOIN")
        {
            throw new NumericException(result.Numeric!.Value, result.Args[_joinErrors0.Contains(result.Verb) ? 1 : 2]);
        }
    }

    public Task PartChannelAsync(string name, CancellationToken cancellationToken)
    {
        return PartChannelAsync(name, null, cancellationToken);
    }

    public async Task PartChannelAsync(string name, string? reason, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!Network.IsConnected)
        {
            throw new InvalidOperationException("Bot is not connected to the network.");
        }

        using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, CancellationSource!.Token);
        var linkedToken = linkedTokenSource.Token;

        var result = await SendAsync("PART", [name, reason], linkedToken).WithReply(c => c.Verb switch
        {
            // channel is 2nd argument
            // TODO: compare strings using server-specified charset case insensitively
            "403" or "442" => c.Args[1] == name,
            // channel is 1st argument (might be a channel list) but verify source as well
            // TODO: compare against Source by extracting our nick if it's a full nick!user@host
            "PART" => c.Args[0].Split(',').Contains(name) && c.Source == Network.Nick,
            _ => false
        }).ConfigureAwait(false);

        if (result.Verb != "PART")
        {
            throw new NumericException(result.Numeric!.Value, result.Args[2]);
        }
    }

    private void OnDisconnected(object? sender, NetworkEventArgs e)
    {
        if (e.Exception != null)
        {
            DisconnectionSource.SetException(e.Exception);
        }
        else
        {
            DisconnectionSource.SetResult();
        }
    }

    private async void OnCommandReceived(CommandEventArgs e)
    {
        // Only handle user commands if we're fully initialized;
        // this ensures all necessary state is in place before command processing
        // Also only handle commands sent as PRIVMSG, not NOTICE as well as reject invalid/corrupted commands
        if (!Initialized || CancellationSource == null || e.Command.Verb != "PRIVMSG" || e.Command.Source == null || e.Command.Args.Count < 2)
        {
            return;
        }

        if (e.Token.IsCancellationRequested || CancellationSource.Token.IsCancellationRequested == true)
        {
            // don't want to throw here since exceptions in async void bubble up to the top and aren't catchable
            // instead just no-op
            return;
        }

        // exceptions thrown here after the first await will terminate the process, so ensure we do not throw
        try
        {
            bool toBot = e.Command.Args[0] == Network.Nick;
            bool haveCommand = TryParseCommandAndArgs(e.Command.Args[1].AsSpan(), out var command, out var args, out var fullLine);

            if (haveCommand || toBot)
            {
                using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(CancellationSource.Token, e.Token);
                var commandObj = CommandFactory.CreateCommand(CommandType.Bot, e.Command.Source, command, args, e.Command.Tags);
                // TODO: pass an immutable networkinfo snapshot (probably by making IrcState a record with immutable collections) rather than the full network
                var context = await BotCommandContextFactory.CreateAsync(this, Network, commandObj, fullLine, linkedSource.Token);

                try
                {
                    // Bot commands *can* return data but right now there's nothing we do with return values, so discard them
                    _ = await CommandDispatcher.DispatchAsync(commandObj, context, linkedSource.Token);
                }
                catch (PermissionException ex)
                {
                    // someone missing a permission isn't an error as far as our logging infra is concerned,
                    // so treat this differently from other exception types
                    Logger.LogDebug("The user {Nick} (account {Account}) does not have permission to execute {Command} (missing {Permission})",
                        ex.Nick, ex.Account, ex.Command, ex.Permission);
                    // TODO: make configurable (both in terms of if we present the message, as well as message contents)
                    await SendNoticeAsync(e.Command.Source, "You do not have permission to execute this command.", linkedSource.Token);
                }
                catch (ValidationException ex)
                {
                    if (ex.ValidationAttribute != null && ex.ValidationResult.MemberNames.Any())
                    {
                        Logger.LogDebug("Validation failed for {Command}: {Attribute} did not succeed on parameter {Parameter}",
                            commandObj.Verb, ex.ValidationAttribute.GetType().Name, ex.ValidationResult.MemberNames.First());
                    }

                    if (!string.IsNullOrWhiteSpace(ex.ValidationResult.ErrorMessage))
                    {
                        await SendNoticeAsync(e.Command.Source, ex.ValidationResult.ErrorMessage, linkedSource.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    Logger.LogDebug("Prematurely terminating {Command} handler due to cancellation request", command);
                    return;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "An error occurred while executing the command {Command}", command);
                }
            }
        }
        // Exceptions caught here will be due to one of two reasons:
        // 1. An exception was thrown in one of the inner exception handlers (e.g. linkedSource.Token got cancelled)
        // 2. Initialization code had an exception
        // Of these, the task cancelled case is not exceptional as that is just bad timing; do not log that case
        catch (OperationCanceledException) { /* no-op */ }
        catch (Exception ex)
        {
            Logger.LogError(ex, "A top-level error occurred in the bot command handler. This likely indicates a bug in the Netwolf.BotFramework library.");
        }
    }

    private bool TryParseCommandAndArgs(ReadOnlySpan<char> line, out string command, out string[] args, out string fullLine)
    {
        bool haveCommand = false;
        string nickPrefix = $"{Network.Nick}: ";

        if (line.StartsWith(Options.CommandPrefix))
        {
            haveCommand = true;
            line = line[Options.CommandPrefix.Length..];
        }
        else if (line.StartsWith(nickPrefix))
        {
            haveCommand = true;
            line = line[nickPrefix.Length..].TrimStart(' ');
        }

        command = string.Empty;
        args = [];
        fullLine = string.Empty;

        if (!haveCommand)
        {
            return false;
        }

        // some extra copying is performed here since the ReadOnlySpan<char>.Split() methods suck in .NET 8
        // As of .NET 9 this API surface is far improved and we can avoid copies until the assignments to command/args/fullLine
        var splitTrimmed = line.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var splitRaw = line.ToString().Split(' ', 2);

        command = splitTrimmed[0];
        
        if (splitTrimmed.Length > 1)
        {
            args = splitTrimmed[1..];
        }

        if (splitRaw.Length == 2)
        {
            fullLine = splitRaw[1];
        }
        
        return true;
    }

    private bool ShouldEnableCap(CapEventArgs e)
    {
        return CapProviders.Any(prov => prov.ShouldEnable(e.CapName, e.CapValue));
    }

    internal async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Set up CancellationSource
        CancellationSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var linkedToken = CancellationSource.Token;

        // Connect to the network
        await Network.ConnectAsync(linkedToken).ConfigureAwait(false);

        // Oper up if necessary; regular oper comes first since soper might require us to be an oper
        if (Options.OperName != null)
        {
            // RSA.Create() isn't supported on browsers, so skip challenge support on them
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Create("browser")) && Options.ChallengeKeyFile != null)
            {
                await DoChallengeAsync(Options.OperName, Options.ChallengeKeyFile, Options.ChallengeKeyPassword, linkedToken).ConfigureAwait(false);
            }
            else if (Options.OperPassword != null)
            {
                await DoOperAsync(Options.OperName, Options.OperPassword, linkedToken).ConfigureAwait(false);
            }
            else
            {
                Logger.LogWarning("Unable to oper as no password or challenge key were provided");
            }
        }

        // attempt soper
        if (Options.ServiceOperPassword != null)
        {
            await DoServicesOperAsync(Options.ServiceOperCommand, Options.ServiceOperPassword, linkedToken).ConfigureAwait(false);
        }

        // Join configured channels
        List<Task> joinTasks = [];
        foreach (var channel in Options.Channels)
        {
            if (channel.Split(' ', 2) is [var name, var key])
            {
                joinTasks.Add(JoinChannelAsync(name, key, linkedToken));
            }
            else
            {
                joinTasks.Add(JoinChannelAsync(channel, linkedToken));
            }
        }

        if (!Task.WaitAll([.. joinTasks], Options.JoinTimeout, CancellationToken.None))
        {
            Logger.LogWarning("Initial JOINs are taking longer than {Time}ms to resolve. This could be a bug, please investigate further.", Options.JoinTimeout);
        }

        Initialized = true;
        Logger.LogInformation("Bot initialization successful");

        // Pause execution task until we're disconnected to prevent overall process from terminating early
        // Waiting on stoppingToken directly rather than linkedToken is intentional; linkedToken is cancelled when DisconnectAsync() is called,
        // which is before the proper completion is fired for DisconnectionSource. stoppingToken can be used to indicate an unclean shutdown.
        await DisconnectionSource.Task.WaitAsync(stoppingToken).ConfigureAwait(false);
    }

    private static readonly HashSet<int> _operNumerics = [381, 461, 464, 491];
    private async Task DoOperAsync(string username, string password, CancellationToken cancellationToken)
    {
        using var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // time out after a few seconds so that we don't hang forever in case we get some non-standard error response (e.g. via NOTICE)
        cancellationSource.CancelAfter(TimeSpan.FromSeconds(5));

        var result = await SendAsync("OPER", [username, password], cancellationSource.Token)
            .WithReply(c => _operNumerics.Contains(c.Numeric ?? -1))
            .ConfigureAwait(false);

        if (result.Numeric == 381)
        {
            Logger.LogTrace("Successfully opered as {name}", username);
        }
        else
        {
            Logger.LogWarning("Unable to oper as {name}: {message}", username, result.UnprefixedCommandPart);
        }
    }

    private static readonly HashSet<int> _challengeIncludeNumerics = [381, 461, 464, 491, 740, 741];
    private static readonly HashSet<int> _challengeEndNumerics = [381, 461, 464, 491, 741];
    private async Task DoChallengeAsync(string username, string filePath, string? filePassword, CancellationToken cancellationToken)
    {
        using var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // time out after a few seconds so that we don't hang forever in case we get some non-standard error response (e.g. via NOTICE)
        cancellationSource.CancelAfter(TimeSpan.FromSeconds(5));

        using var rsa = RSA.Create();
        var key = await File.ReadAllTextAsync(filePath, cancellationToken);

        if (filePassword != null)
        {
            rsa.ImportFromEncryptedPem(key, filePassword);
        }
        else
        {
            rsa.ImportFromPem(key);
        }

        var results = await SendAsync("CHALLENGE", [username], cancellationToken)
            .WithReplies(c => _challengeIncludeNumerics.Contains(c.Numeric ?? -1), c => _challengeEndNumerics.Contains(c.Numeric ?? -1))
            .ToObservable()
            .ToList()
            .ToTask(cancellationToken);

        var result = results.Last();

        if (result.Numeric == 741)
        {
            var encryptedChallenge = Convert.FromBase64String(string.Join("", results.Where(c => c.Numeric == 740).Select(c => c.Args[1])));
            var plainChallenge = rsa.Decrypt(encryptedChallenge, RSAEncryptionPadding.OaepSHA1);
            var hashedChallenge = SHA1.HashData(plainChallenge);
            var response = Convert.ToBase64String(hashedChallenge);
            result = await SendAsync("CHALLENGE", [$"+{response}"], cancellationToken)
                .WithReply(c => _challengeEndNumerics.Contains(c.Numeric ?? -1))
                .ConfigureAwait(false);
        }

        switch (result.Numeric)
        {
            // TODO: Get a general-purpose Numeric enum perhaps? There's one in Netwolf.Server but that's only the numerics the server component sends
            // whereas from a client perspective we'd want wide support across all ircds
            case 461:
            case 464:
            case 491:
                Logger.LogWarning("Unable to challenge as {name}: {message}", username, result.UnprefixedCommandPart);
                break;
            case 381:
                Logger.LogTrace("Successfully opered as {name}", username);
                break;
        }
    }

    private async Task DoServicesOperAsync(string command, string password, CancellationToken cancellationToken)
    {
        await Network.UnsafeSendRawAsync(string.Format(command, password), cancellationToken).ConfigureAwait(false);

        // there is no standard way to determine if this command was successful so just wait a few seconds and move on
        // TODO: introduce an option of a string to look for in the event of a successful soper, and assume we failed if we don't get that by the timeout
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        CommandSubscription.Dispose();
        CancellationSource?.Dispose();
        await Network.DisposeAsync().ConfigureAwait(false);
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
                CommandSubscription.Dispose();
                // CancellationSource could be null if we dispose before calling ExecuteAsync, despite it being marked non-nullable
                CancellationSource?.Dispose();
                Network.Dispose();
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
}
