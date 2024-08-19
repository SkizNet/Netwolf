﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Netwolf.BotFramework.Exceptions;
using Netwolf.BotFramework.Internal;
using Netwolf.Transport.IRC;

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
public abstract class Bot : IBot
{
    protected ILogger<Bot> Logger { get; init; }

    protected BotOptions Options { get; init; }

    protected INetwork Network { get; init; }

    private object Lock { get; init; } = new();

    private TaskCompletionSource DisconnectionSource { get; init; }

    private PendingTaskCollection PendingTasks { get; init; } = new();

    /// <summary>
    /// Public constructor. If making your own constructor ensure it has a <c>string botName</c>
    /// parameter as its last element; otherwise <see cref="ActivatorUtilities.CreateInstance"/> will
    /// not be able to find your constructor.
    /// </summary>
    /// <param name="logger">Logger</param>
    /// <param name="options">Bot options</param>
    /// <param name="networkFactory">Network factory</param>
    /// <param name="botName">Internal bot name passed to <see cref="BotFrameworkExtensions.AddBot"/></param>
    public Bot(
        ILogger<Bot> logger,
        IOptionsSnapshot<BotOptions> options,
        INetworkFactory networkFactory,
        string botName)
    {
        Logger = logger;
        Options = options.Get(botName);
        Network = networkFactory.Create(botName, Options);
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
        // cancel all pending operations
        PendingTasks.Dispose();

        // send a QUIT
        await Network.DisconnectAsync(reason);
    }

    public Task JoinChannelAsync(string name, CancellationToken cancellationToken)
    {
        return JoinChannelAsync(name, null, cancellationToken);
    }

    public async Task JoinChannelAsync(string name, string? key = null, CancellationToken cancellationToken = default)
    {
        // TODO: validate that the channel name starts with a channel prefix character
        // (and have a different API for /join 0 -- aka part all channels)
        using var pendingTask = PendingTasks.Create();
        var opSource = pendingTask.Source;

        void handler(object? sender, NetworkEventArgs args)
        {
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
        await Network.SendAsync(command, cancellationToken).ConfigureAwait(false);

        try
        {
            await opSource.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
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

    private void OnCommandReceived(object? sender, NetworkEventArgs e)
    {
        throw new NotImplementedException();
    }

    private void OnCapReceived(object? sender, CapEventArgs e)
    {
        throw new NotImplementedException();
    }

    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Connect to the network
        await Network.ConnectAsync(stoppingToken).ConfigureAwait(false);

        // Oper up if necessary; regular oper comes first since soper might require us to be an oper
        var operCompletionSource = new TaskCompletionSource();
        if (Options.OperName != null)
        {
            // RSA.Create() isn't supported on browsers, so skip challenge support on them
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Create("browser")) && Options.ChallengeKeyFile != null)
            {
                await DoChallengeAsync(Options.OperName, Options.ChallengeKeyFile, Options.ChallengeKeyPassword, operCompletionSource, stoppingToken).ConfigureAwait(false);
            }
            else if (Options.OperPassword != null)
            {
                await DoOperAsync(Options.OperName, Options.OperPassword, operCompletionSource, stoppingToken).ConfigureAwait(false);
            }
            else
            {
                Logger.LogWarning("Unable to oper as no password or challenge key were provided");
                operCompletionSource.SetResult();
            }
        }

        await operCompletionSource.Task.WaitAsync(stoppingToken).ConfigureAwait(false);

        // attempt soper
        operCompletionSource = new();
        if (Options.ServiceOperPassword != null)
        {
            await DoServicesOperAsync(Options.ServiceOperCommand, Options.ServiceOperPassword, operCompletionSource, stoppingToken).ConfigureAwait(false);
        }

        await operCompletionSource.Task.WaitAsync(stoppingToken).ConfigureAwait(false);

        // Join configured channels
        foreach (var channel in Options.Channels)
        {
            if (channel.Split(' ', 2) is [var name, var key])
            {
                _ = JoinChannelAsync(name, key, stoppingToken);
            }
            else
            {
                _ = JoinChannelAsync(channel, stoppingToken);
            }
        }

        // Pause execution task until we're disconnected to prevent overall process from terminating early
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
}