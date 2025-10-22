// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Netwolf.Attributes;
using Netwolf.BotFramework.Exceptions;
using Netwolf.BotFramework.Internal;
using Netwolf.BotFramework.Services;
using Netwolf.PluginFramework.Commands;
using Netwolf.Transport.Commands;
using Netwolf.Transport.Events;
using Netwolf.Transport.Exceptions;
using Netwolf.Transport.IRC;
using Netwolf.Transport.State;

using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
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
    #region Public properties
    /// <summary>
    /// Name of the bot
    /// </summary>
    public string BotName { get; private init; }

    /// <summary>
    /// Information on the Bot's network.
    /// </summary>
    public INetworkInfo NetworkInfo => Network.AsNetworkInfo();
    #endregion

    #region Protected properties and DI services
    /// <summary>
    /// Logger used to log events
    /// </summary>
    protected ILogger<Bot> Logger { get; private init; }

    /// <summary>
    /// A snapshot of the bot options as defined by configuration
    /// </summary>
    protected BotOptions Options => OptionsMonitor.Get(BotName);

    /// <summary>
    /// The network's underlying command stream
    /// </summary>
    protected internal IObservable<ICommand> CommandStream => Network.CommandReceived.Select(e => e.Command);
    #endregion

    #region Private properties and fields
    /// <summary>
    /// Disposed flag
    /// </summary>
    private bool _disposed = false;

    /// <summary>
    /// Configuration monitor to access reloadable config
    /// </summary>
    private IOptionsMonitor<BotOptions> OptionsMonitor { get; init; }

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
    /// Subscriptions to the underlying <see cref="Network"/>'s command event stream
    /// </summary>
    private List<IDisposable> CommandSubscriptions { get; init; } = [];

    /// <summary>
    /// Allows for cancelling all outstanding Tasks when <see cref="DisconnectAsync(string)"/> is called.
    /// </summary>
    private CancellationTokenSource? CancellationSource { get; set; }
    #endregion

    #region Private DI services
    private ICommandDispatcher<BotCommandResult> CommandDispatcher { get; init; }

    private ICommandFactory CommandFactory { get; init; }

    private BotCommandContextFactory BotCommandContextFactory { get; init; }

    /// <summary>
    /// Additional CAPs that may be supported, whether conditionally by us or by user-defined code
    /// </summary>
    private IEnumerable<ICapProvider> CapProviders { get; init; }
    #endregion

    #region Constructors
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
        CapProviders = data.CapProviders;
        DisconnectionSource = new();

        // Register our network events
        Network.CapFilter += ShouldEnableCap;
        CommandSubscriptions.Add(Network.CommandReceived.Subscribe(OnCommandReceived));
        Network.NetworkDisconnected += OnDisconnected;

        // Wire up bot commands
        // There is a reflection-based slow path and a source generator fast path; detect which was used
        var generatedTypes = GetType()
            .Assembly
            .GetCustomAttributes<SourceGeneratedCommandAttribute>()
            .Where(attr => attr.TargetType == GetType() && attr.HandlerType == typeof(BotCommandResult))
            .ToList();

        if (generatedTypes.Count > 0 && data.EnableCommandOptimization)
        {
            // fast path
            generatedTypes.ForEach(attr => CommandDispatcher.AddCommand(attr.GeneratedType));
        }
        else if (!data.ForceCommandOptimization)
        {
            // slow path
            var commands = GetType()
                .GetMethods()
                .Select(method => (method, attr: method.GetCustomAttribute<CommandAttribute>()))
                .Where(o => o.attr != null)
                .Select(o => new BotCommandThunk(this, o.method, o.attr!));

            foreach (var command in commands)
            {
                CommandDispatcher.AddCommand(command);
            }
        }
        else
        {
            // for unit testing only; optimization has been forced but doesn't exist for this command
            // throw an error instead of generating a thunk
            throw new InvalidOperationException($"Optimization forced but was either not enabled or we found no generated types (discovered {generatedTypes.Count}).");
        }
    }
    #endregion

    #region Command sending
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
        return Network.SendRawAsync(rawLine, cancellationToken);
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

        var command = CommandFactory.PrepareClientCommand(NetworkInfo.Self, verb, args, tags, CommandCreationOptions.MakeOptions(NetworkInfo));
        return Network.SendAsync(command, cancellationToken);
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
        string? sharedChannel = null;
        if (NetworkInfo.TryGetISupport(ISupportToken.CPRIVMSG, out _) && NetworkInfo.GetUserByNick(target) is UserRecord targetUser)
        {
            sharedChannel = NetworkInfo.GetChannelsForUser(targetUser)
                .Select(x => x.Key)
                .FirstOrDefault(x => !string.IsNullOrEmpty(NetworkInfo.Channels.GetValueOrDefault(x)))
                ?.Name;
        }

        var options = CommandCreationOptions.MakeOptions(NetworkInfo);
        foreach (var command in CommandFactory.PrepareClientMessage(NetworkInfo.Self, MessageType.Message, target, message, tags, sharedChannel, options))
        {
            await Network.SendAsync(command, cancellationToken).ConfigureAwait(false);
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
        string? sharedChannel = null;
        if (NetworkInfo.TryGetISupport(ISupportToken.CNOTICE, out _) && NetworkInfo.GetUserByNick(target) is UserRecord targetUser)
        {
            sharedChannel = NetworkInfo.GetChannelsForUser(targetUser)
                .Select(x => x.Key)
                .FirstOrDefault(x => !string.IsNullOrEmpty(NetworkInfo.Channels.GetValueOrDefault(x)))
                ?.Name;
        }

        var options = CommandCreationOptions.MakeOptions(NetworkInfo);
        foreach (var command in CommandFactory.PrepareClientMessage(NetworkInfo.Self, MessageType.Notice, target, message, tags, sharedChannel, options))
        {
            await Network.SendAsync(command, cancellationToken).ConfigureAwait(false);
        }
    }
    #endregion

    #region Channel commands
    public Task JoinChannelAsync(string name, CancellationToken cancellationToken)
    {
        return JoinChannelAsync(name, null, cancellationToken);
    }

    private static readonly string[] _joinErrors1 = ["403", "405", "471", "473", "474", "475"];
    private static readonly string[] _joinErrors0 = ["476"];

    public async Task JoinChannelAsync(string name, string? key, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!Network.IsConnected)
        {
            throw new InvalidOperationException("Bot is not connected to the network.");
        }

        ArgumentException.ThrowIfNullOrEmpty(name);
        if (!NetworkInfo.ChannelTypes.Contains(name[0]))
        {
            throw new ArgumentException($"Channel name is not a valid for this network.", nameof(name));
        }

        // it's (mostly) safe to continue to use linkedToken after linkedTokenSource is disposed
        // linkedToken.WaitHandle will throw an exception but simply checking cancellation state is safe
        // CancellationSource is guaranteed non-null here since ExecuteAsync() populates it and that was guaranteed to run first in order to get this far
        using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, CancellationSource!.Token);
        var linkedToken = linkedTokenSource.Token;

        var result = await SendAsync("JOIN", [name, key], linkedToken).WithReply(c => c.Verb switch
        {
            var x when _joinErrors0.Contains(x) => IrcUtil.IrcEquals(c.Args[0], name, NetworkInfo.CaseMapping),
            var x when _joinErrors1.Contains(x) => IrcUtil.IrcEquals(c.Args[1], name, NetworkInfo.CaseMapping),
            "JOIN" => IrcUtil.IrcEquals(c.Args[0], name, NetworkInfo.CaseMapping)
                && IrcUtil.IrcEquals(IrcUtil.SplitHostmask(c.Source ?? string.Empty).Nick, NetworkInfo.Nick, NetworkInfo.CaseMapping),
            _ => false
        }).ConfigureAwait(false);

        if (result.Verb != "JOIN")
        {
            throw new NumericException(result.Numeric!.Value, result.Args[_joinErrors0.Contains(result.Verb) ? 1 : 2]);
        }

        // issue a WHOX (or WHO if WHOX isn't available) to obtain more details than NAMES is able to give
        if (NetworkInfo.TryGetISupport(ISupportToken.WHOX, out _))
        {
            string token = Random.Shared.Next(1000).ToString();
            var results = SendAsync("WHO", [name, $"%tcuhnfar,{token}"], linkedToken).WithReplies(
                c => c.Verb == "354" && c.Args.Count == 9 && c.Args[1] == token && IrcUtil.IrcEquals(c.Args[2], name, NetworkInfo.CaseMapping),
                c => c.Verb == "315" && IrcUtil.IrcEquals(c.Args[1], name, NetworkInfo.CaseMapping)
                );

            await foreach (var c in results)
            {
                OnWhoXReply(c);
            }
        }
        else
        {
            _ = await SendAsync("WHO", [name], linkedToken)
                .WithReply(c => c.Verb == "315" && IrcUtil.IrcEquals(c.Args[1], name, NetworkInfo.CaseMapping))
                .ConfigureAwait(false);
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
            "403" or "442" => IrcUtil.IrcEquals(c.Args[1], name, NetworkInfo.CaseMapping),
            // channel is 1st argument (might be a channel list) but verify source as well
            "PART" => IrcUtil.CommaListContains(c.Args[0], name, NetworkInfo.CaseMapping)
                && IrcUtil.IrcEquals(IrcUtil.SplitHostmask(c.Source ?? string.Empty).Nick, NetworkInfo.Nick, NetworkInfo.CaseMapping),
            _ => false
        }).ConfigureAwait(false);

        if (result.Verb != "PART")
        {
            throw new NumericException(result.Numeric!.Value, result.Args[2]);
        }
    }
    #endregion

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

    private void OnDisconnected(object? sender, NetworkEventArgs e)
    {
        if (!ReferenceEquals(e.Network, Network))
        {
            // wrong network
            return;
        }
        else if (e.Exception != null)
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

        // ignore messages sent *from* the bot (e.g. echo-message CAP is enabled)
        var sender = NetworkInfo.GetUserByNick(IrcUtil.SplitHostmask(e.Command.Source).Nick);
        if (sender?.Id == NetworkInfo.ClientId)
        {
            return;
        }

        // exceptions thrown here after the first await will terminate the process, so ensure we do not throw
        try
        {
            bool toBot = e.Command.Args[0] == NetworkInfo.Nick;
            bool haveCommand = TryParseCommandAndArgs(e.Command.Args[1].AsSpan(), out var command, out var args);

            if (haveCommand || toBot)
            {
                using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(CancellationSource.Token, e.Token);
                var commandObj = CommandFactory.CreateCommand(CommandType.Bot, e.Command.Source, command, args, e.Command.Tags);
                var context = await BotCommandContextFactory.CreateAsync(this, e.Command.Args[0], commandObj, e.Command.Args[1], linkedSource.Token);

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

    private bool TryParseCommandAndArgs(ReadOnlySpan<char> line, out string command, out string[] args)
    {
        bool haveCommand = false;
        string nickPrefix = $"{NetworkInfo.Nick}: ";

        if (line.StartsWith(Options.CommandPrefix))
        {
            line = line[Options.CommandPrefix.Length..];
            haveCommand = line.Length > 0 && line[0] != ' ';
        }
        else if (line.StartsWith(nickPrefix))
        {
            line = line[nickPrefix.Length..].TrimStart(' ');
            haveCommand = line.Length > 0;
        }

        if (!haveCommand)
        {
            command = string.Empty;
            args = [];
            return false;
        }

        Span<Range> rawSplits = stackalloc Range[2];
        int num = line.Split(rawSplits, ' ');
        command = line[rawSplits[0]].ToString();
        args = num == 2
            ? [line[rawSplits[1]].ToString()]
            : [];

        return true;
    }

    private ValueTask<bool> ShouldEnableCap(object? sender, CapEventArgs e)
    {
        return new(CapProviders.Any(prov => prov.ShouldEnable(e.CapName, e.CapValue)));
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

        var delay = Task.Delay(Options.JoinTimeout, CancellationToken.None);
        await Task.WhenAny(Task.WhenAll(joinTasks), delay);
        if (delay.Status == TaskStatus.RanToCompletion)
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
            .ToTask(cancellationToken)
            .ConfigureAwait(false);

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
        await UnsafeSendRawAsync(string.Format(command, password), cancellationToken).ConfigureAwait(false);

        // there is no standard way to determine if this command was successful so just wait a few seconds and move on
        // TODO: introduce an option of a string to look for in the event of a successful soper, and assume we failed if we don't get that by the timeout
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
    }

    #region IDisposable / IAsyncDisposable
    protected virtual async ValueTask DisposeAsyncCore()
    {
        CommandSubscriptions.ForEach(static s => s.Dispose());
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
                CommandSubscriptions.ForEach(static s => s.Dispose());
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
    #endregion

    // Note: no ServerCommandAttribute for WHOX replies since there's no easy way to determine which set of fields were requested
    // This particular implementation assumes a mask of %tcuhnfar. The token is not checked.
    // RPL_WHOSPCRPL (354) <client> [token] [channel] [user] [ip] [host] [server] [nick] [flags] [hopcount] [idle] [account] [oplevel] [:realname]
    // this impl: <client> <token> <channel> <user> <host> <nick> <flags> <account> :<realname>
    [SuppressMessage("Style", "IDE0301:Simplify collection initialization",
        Justification = "Immutable*.Empty is more indicative of what the data type is and requires no allocations")]
    private void OnWhoXReply(ICommand command)
    {
        if (NetworkInfo.GetUserByNick(command.Args[5]) is UserRecord user)
        {
            // fill in missing user info
            user = user with
            {
                Ident = command.Args[3],
                Host = command.Args[4],
                Account = command.Args[7] == "0" ? null : command.Args[7],
                RealName = command.Args[8],
                IsAway = command.Args[6][0] == 'G',
            };
        }
        else
        {
            user = new UserRecord(
                Guid.NewGuid(),
                command.Args[5],
                command.Args[3],
                command.Args[4],
                command.Args[7] == "0" ? null : command.Args[7],
                command.Args[6][0] == 'G',
                command.Args[8],
                ImmutableHashSet<char>.Empty,
                ImmutableDictionary<Guid, string>.Empty);
        }

        // channel being null is ok here since /who nick returns an arbitrary channel or potentially a '*'
        var channel = command.Args[2] == "*" ? null : NetworkInfo.GetChannel(command.Args[2]);

        // if channel isn't known and this user didn't share any other channels with us, then nothing to do here
        if (channel == null && user.Channels.Count == 0)
        {
            return;
        }

        // add prefix if channel is known
        if (channel != null)
        {
            // determine prefix
            int prefixStart = (command.Args[6].Length == 1 || command.Args[6][1] != '*') ? 1 : 2;
            string prefix = string.Concat(command.Args[6][prefixStart..].TakeWhile(NetworkInfo.ChannelPrefixSymbols.Contains));

            user = user with { Channels = user.Channels.SetItem(channel.Id, prefix) };
        }

        // update network state with the updated user info
        Network.UnsafeUpdateUser(user);
    }
}
