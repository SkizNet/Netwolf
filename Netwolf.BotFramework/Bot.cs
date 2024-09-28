using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Netwolf.BotFramework.Exceptions;
using Netwolf.BotFramework.Internal;
using Netwolf.PluginFramework.Commands;
using Netwolf.Transport.IRC;

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Data.Common;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

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

    /// <summary>
    /// A snapshot of the bot options as defined by configuration
    /// </summary>
    protected BotOptions Options { get; private init; }

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
    /// Allows for cancelling all outstanding Tasks when <see cref="DisconnectAsync(string)"/> is called.
    /// </summary>
    private CancellationTokenSource? CancellationSource { get; set; }

    private ICommandDispatcher<BotCommandResult> CommandDispatcher { get; init; }

    private ICommandFactory CommandFactory { get; init; }

    /// <summary>
    /// Public constructor. If making your own constructor ensure it has a <c>string botName</c>
    /// parameter as its last element; otherwise <see cref="ActivatorUtilities.CreateInstance"/> will
    /// not be able to find your constructor.
    /// </summary>
    /// <param name="logger">Logger</param>
    /// <param name="options">Bot options</param>
    /// <param name="networkFactory">Network factory</param>
    /// <param name="commandDispatcher"></param>
    /// <param name="commandFactory"></param>
    /// <param name="botName">Internal bot name passed to <see cref="BotFrameworkExtensions.AddBot"/></param>
    public Bot(
        ILogger<Bot> logger,
        IOptionsSnapshot<BotOptions> options,
        INetworkFactory networkFactory,
        ICommandDispatcher<BotCommandResult> commandDispatcher,
        ICommandFactory commandFactory,
        string botName)
    {
        BotName = botName;
        Logger = logger;
        Options = options.Get(botName);
        Network = networkFactory.Create(botName, Options);
        CommandDispatcher = commandDispatcher;
        CommandFactory = commandFactory;
        DisconnectionSource = new();

        Network.CapReceived += OnCapReceived;
        Network.CommandReceived += OnCommandReceived;
        Network.Disconnected += OnDisconnected;
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

    public async Task JoinChannelAsync(string name, string? key = null, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!Network.IsConnected)
        {
            throw new InvalidOperationException("Bot is not connected to the network.");
        }

        // TODO: validate that the channel name starts with a channel prefix character
        // (and have a different API for /join 0 -- aka part all channels)
        TaskCompletionSource opSource = new();

        // it's (mostly) safe to continue to use linkedToken after linkedTokenSource is disposed
        // linkedToken.WaitHandle will throw an exception but simply checking cancellation state is safe
        // CancellationSource is guaranteed non-null here since ExecuteAsync() populates it and that was guaranteed to run first in order to get this far
        using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, CancellationSource!.Token);
        var linkedToken = linkedTokenSource.Token;

        linkedToken.ThrowIfCancellationRequested();

        void handler(object? sender, NetworkEventArgs args)
        {
            if (linkedToken.IsCancellationRequested)
            {
                opSource.SetCanceled(linkedToken);
                return;
            }

            switch (args.Command?.Verb)
            {
                case "403":
                case "405":
                case "471":
                case "473":
                case "474":
                case "475":
                    // channel is 2nd argument
                    // TODO: compare strings using server-specified charset case insensitively
                    if (args.Command.Args[1] == name)
                    {
                        throw new NumericException(args.Command.Numeric!.Value, args.Command.Args[2]);
                    }
                    
                    break;
                case "476":
                    // channel is 1st argument
                    if (args.Command.Args[0] == name)
                    {
                        throw new NumericException(args.Command.Numeric!.Value, args.Command.Args[1]);
                    }

                    break;
                case "JOIN":
                    // channel is 1st argument but verify source as well
                    // TODO: compare against Source by extracting our nick if it's a full nick!user@host
                    if (args.Command.Args[0] == name && args.Command.Source == Network.Nick)
                    {
                        opSource.SetResult();
                    }

                    break;
            }
        }
        
        Network.CommandReceived += handler;
        var command = Network.PrepareCommand("JOIN", [name, key]);
        await Network.SendAsync(command, linkedToken).ConfigureAwait(false);

        try
        {
            await opSource.Task.WaitAsync(linkedToken).ConfigureAwait(false);
        }
        finally
        {
            Network.CommandReceived -= handler;
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

    private async void OnCommandReceived(object? sender, NetworkEventArgs e)
    {
        // Only handle user commands if we're fully initialized;
        // this ensures all necessary state is in place before command processing
        // Also only handle commands sent as PRIVMSG, not NOTICE
        if (!Initialized || e.Command!.Verb != "PRIVMSG")
        {
            return;
        }

        e.Token.ThrowIfCancellationRequested();
        CancellationSource!.Token.ThrowIfCancellationRequested();
        
        bool toBot = e.Command!.Args[0] == Network.Nick;
        bool haveCommand = TryParseCommandAndArgs(e.Command!.Args[1].AsSpan(), out var command, out var args, out var fullLine);

        if (haveCommand || toBot)
        {
            using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(CancellationSource.Token, e.Token);
            var commandObj = CommandFactory.CreateCommand(CommandType.Bot, e.Command.Source, command, args, e.Command.Tags);
            var context = new BotCommandContext(this, fullLine);

            try
            {
                _ = await CommandDispatcher.DispatchAsync(commandObj, context, linkedSource.Token);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An error occurred while executing the command {Command}", command);
            }
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

    private void OnCapReceived(object? sender, CapEventArgs e)
    {
        throw new NotImplementedException();
    }

    internal async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Set up CancellationSource
        CancellationSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var linkedToken = CancellationSource.Token;

        // Connect to the network
        await Network.ConnectAsync(linkedToken).ConfigureAwait(false);

        // Oper up if necessary; regular oper comes first since soper might require us to be an oper
        var operCompletionSource = new TaskCompletionSource();
        if (Options.OperName != null)
        {
            // RSA.Create() isn't supported on browsers, so skip challenge support on them
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Create("browser")) && Options.ChallengeKeyFile != null)
            {
                await DoChallengeAsync(Options.OperName, Options.ChallengeKeyFile, Options.ChallengeKeyPassword, operCompletionSource, linkedToken).ConfigureAwait(false);
            }
            else if (Options.OperPassword != null)
            {
                await DoOperAsync(Options.OperName, Options.OperPassword, operCompletionSource, linkedToken).ConfigureAwait(false);
            }
            else
            {
                Logger.LogWarning("Unable to oper as no password or challenge key were provided");
                operCompletionSource.SetResult();
            }
        }

        await operCompletionSource.Task.WaitAsync(linkedToken).ConfigureAwait(false);

        // attempt soper
        operCompletionSource = new();
        if (Options.ServiceOperPassword != null)
        {
            await DoServicesOperAsync(Options.ServiceOperCommand, Options.ServiceOperPassword, operCompletionSource, linkedToken).ConfigureAwait(false);
        }

        await operCompletionSource.Task.WaitAsync(linkedToken).ConfigureAwait(false);

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

    private async Task DoOperAsync(string username, string password, TaskCompletionSource completionSource, CancellationToken cancellationToken)
    {
        // Set up command handler
        void handler(object? sender, NetworkEventArgs args)
        {
            switch (args.Command?.Numeric)
            {
                // TODO: Get a general-purpose Numeric enum perhaps? There's one in Netwolf.Server but that's only the numerics the server component sends
                // whereas from a client perspective we'd want wide support across all ircds
                // Also maybe get rid of the Numeric property in Command and make the Numeric thing string-based
                case 461:
                case 464:
                case 491:
                    Logger.LogWarning("Unable to oper as {name}: {message}", username, args.Command.UnprefixedCommandPart);
                    Network.CommandReceived -= handler;
                    completionSource.TrySetResult();
                    break;
                case 381:
                    Logger.LogTrace("Successfully opered as {name}", username);
                    Network.CommandReceived -= handler;
                    completionSource.TrySetResult();
                    break;
            }
        }

        Network.CommandReceived += handler;

        try
        {
            var command = Network.PrepareCommand("OPER", [username, password]);
            await Network.SendAsync(command, cancellationToken).ConfigureAwait(false);

            // time out after a few seconds so that we don't hang forever in case we get some non-standard error response (e.g. via NOTICE)
            // TODO: assume oper fails after this timeout and give the bot an opportunity to fail fast if it requires oper
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Network.CommandReceived -= handler;
            completionSource.TrySetResult();
        }
    }

    private async Task DoChallengeAsync(string username, string filePath, string? filePassword, TaskCompletionSource completionSource, CancellationToken cancellationToken)
    {
        StringBuilder challengeText = new();
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

        async void handler(object? sender, NetworkEventArgs args)
        {
            switch (args.Command?.Numeric)
            {
                // TODO: Get a general-purpose Numeric enum perhaps? There's one in Netwolf.Server but that's only the numerics the server component sends
                // whereas from a client perspective we'd want wide support across all ircds
                case 461:
                case 464:
                case 491:
                    Logger.LogWarning("Unable to challenge as {name}: {message}", username, args.Command.UnprefixedCommandPart);
                    Network.CommandReceived -= handler;
                    completionSource.TrySetResult();
                    break;
                case 381:
                    Logger.LogTrace("Successfully opered as {name}", username);
                    Network.CommandReceived -= handler;
                    completionSource.TrySetResult();
                    break;
                case 740:
                    challengeText.Append(args.Command.Args[1]);
                    break;
                case 741:
                    var encryptedChallenge = Convert.FromBase64String(challengeText.ToString());
                    var plainChallenge = rsa.Decrypt(encryptedChallenge, RSAEncryptionPadding.OaepSHA1);
                    var hashedChallenge = SHA1.HashData(plainChallenge);
                    var response = Convert.ToBase64String(hashedChallenge);
                    var command = Network.PrepareCommand("CHALLENGE", [$"+{response}"]);
                    await Network.SendAsync(command, cancellationToken).ConfigureAwait(false);
                    break;
            }
        }

        Network.CommandReceived += handler;

        try
        {
            var command = Network.PrepareCommand("CHALLENGE", [username]);
            await Network.SendAsync(command, cancellationToken).ConfigureAwait(false);

            // time out after a few seconds so that we don't hang forever in case we get some non-standard error response (e.g. via NOTICE)
            // TODO: assume oper fails after this timeout and give the bot an opportunity to fail fast if it requires oper
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Network.CommandReceived -= handler;
            completionSource.TrySetResult();
        }
    }

    private async Task DoServicesOperAsync(string command, string password, TaskCompletionSource completionSource, CancellationToken cancellationToken)
    {
        await Network.UnsafeSendRawAsync(string.Format(command, password), cancellationToken).ConfigureAwait(false);

        // there is no standard way to determine if this command was successful so just wait a few seconds and move on
        // TODO: introduce an option of a string to look for in the event of a successful soper, and assume we failed if we don't get that by the timeout
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
        completionSource.SetResult();
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
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
