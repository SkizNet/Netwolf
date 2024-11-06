// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: GPL-3.0-or-later

using Netwolf.BotFramework.RateLimiting;
using Netwolf.Transport.IRC;

namespace Netwolf.BotFramework;

/// <summary>
/// Configuration settings for a <see cref="Bot"/>.
/// </summary>
public class BotOptions : NetworkOptions
{
    /// <summary>
    /// List of channels the bot should automatically join upon startup.
    /// If any channels require a key (cmode +k), include it in the string separated by a space.
    /// <para />
    /// Example:
    /// <code>
    /// Channels = [
    ///     "#foo",
    ///     // join #bar with channel key baz
    ///     "#bar baz",
    ///     // all channel prefixes are supported
    ///     "&amp;qux"
    /// ]
    /// </code>
    /// </summary>
    public string[] Channels { get; set; } = [];

    /// <summary>
    /// If set, the bot will attempt to automatically "oper up" after connecting.
    /// Either <see cref="OperPassword"/> or <see cref="ChallengeKeyFile"/> must be set as well
    /// to use either <c>/OPER</c> or <c>/CHALLENGE</c>, respectively. If both are set,
    /// <c>/CHALLENGE</c> is preferred.
    /// </summary>
    public string? OperName { get; set; }

    /// <summary>
    /// Oper password, when using <c>/OPER</c>.
    /// </summary>
    public string? OperPassword { get; set; }

    /// <summary>
    /// Path to the RSA challenge key, in PEM format, when using <c>/CHALLENGE</c>.
    /// May be encrypted, in which case <see cref="ChallengeKeyPassword"/> is used to decrypt the file.
    /// </summary>
    public string? ChallengeKeyFile { get; set; }

    /// <summary>
    /// Password for the challenge key, if any.
    /// </summary>
    public string? ChallengeKeyPassword { get; set; }

    /// <summary>
    /// If set, the bot will attempt to automatically identify to OperServ with the given password.
    /// <see cref="ServiceOperCommand"/> controls the command sent.
    /// </summary>
    public string? ServiceOperPassword { get; set; }

    /// <summary>
    /// Raw IRC protocol command used to authenticate as a services oper. <c>{password}</c> is
    /// a placeholder that will be replaced with <see cref="ServiceOperPassword"/> at runtime.
    /// </summary>
    public string ServiceOperCommand { get; set; } = "PRIVMSG OperServ :IDENTIFY {password}";

    /// <summary>
    /// How long (in milliseconds) to wait for all JOINs to complete on bot startup before
    /// proceeding anyway. If joins take longer than this to succeed or fail, this could
    /// indicate an issue with how this library handles the ircd or that the ircd is silently
    /// failing the JOIN commands. More investigation should be performed in either scenario
    /// by examining debug logs.
    /// </summary>
    public int JoinTimeout { get; set; } = 30_000;

    /// <summary>
    /// Prefix to look for when checking if in-channel messages are bot commands.
    /// The prefix is optional when addressing the bot via PM.
    /// </summary>
    public string CommandPrefix { get; set; } = "!";

    /// <summary>
    /// Maximum number of commands that can be waiting in a command-based rate limiter queue;
    /// attempts to send more commands while the queue is full will result in dropped commands!
    /// This should be high enough to never be reached in normal operation; the default is 500.
    /// This setting is read once during bot initialization so changes will require bot restarts.
    /// </summary>
    public int RateLimiterMaxCommands { get; set; } = 500;

    /// <summary>
    /// Maximum amount of bytes that can be waiting in a byte-based rate limiter queue;
    /// attempts to send more bytes while the queue is full will result in dropped commands!
    /// This should be high enough to never be reached in normal operation; the default is 2 MiB.
    /// This setting is read once during bot initialization so changes will require bot restarts.
    /// </summary>
    public int RateLimiterMaxBytes { get; set; } = 2_097_152;

    /// <summary>
    /// Rate limiter that tracks each message target independently.
    /// This will only apply to PRIVMSG, NOTICE, and TAGMSG commands.
    /// This setting is read once during bot initialization so changes will require bot restarts.
    /// </summary>
    public TokenBucketConfig DefaultPerTargetLimiter { get; set; } = new();

    /// <summary>
    /// Per-target overrides of the generic <see cref="DefaultPerTargetLimiter"/>.
    /// If a target is set here, it will take precedence over the default generic per-target config.
    /// This setting is read once during bot initialization so changes will require bot restarts.
    /// </summary>
    public Dictionary<string, TokenBucketConfig> PerTargetLimiter { get; set; } = [];

    /// <summary>
    /// Rate limiter that applies to sent commands on a per-command basis.
    /// Commands may additionally have arities attached so that there are individual limiters
    /// based on the number of command arguments (e.g. to have different limits for local vs. remote WHOIS).
    /// Commands with arities should be specified with a backtick and the arity, such as WHOIS`2 for remote WHOIS.
    /// This setting is read once during bot initialization so changes will require bot restarts.
    /// </summary>
    public Dictionary<string, SlidingWindowConfig> PerCommandLimiter { get; set; } = [];

    /// <summary>
    /// Rate limiter that applies to all commands sent by the bot.
    /// This setting is read once during bot initialization so changes will require bot restarts.
    /// </summary>
    public TokenBucketConfig GlobalCommandLimiter { get; set; } = new();

    /// <summary>
    /// Rate limiter that applies to all commands sent by the bot.
    /// Unlike <see cref="GlobalCommandLimiter"/>, this is meant to avoid hitting "SendQ" limits
    /// and each command will deduct its final size in bytes from the limiter.
    /// This setting is read once during bot initialization so changes will require bot restarts.
    /// </summary>
    public SlidingWindowConfig GlobalByteLimiter { get; set; } = new();

    /// <summary>
    /// Permissions for accounts recognized by the bot. By default, this is not used.
    /// You need to add an account provider as well as call <see cref="BotFrameworkExtensions.AddSettingsFilePermissionStrategy(IBotBuilder)"/>
    /// to make use of permissions defined here. Keys are account names and values are lists of permissions.
    /// </summary>
    public Dictionary<string, List<string>> Permissions { get; set; } = [];
}
