// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.Extensions.Logging;

using Netwolf.Transport.Commands;
using Netwolf.Transport.Exceptions;
using Netwolf.Transport.Extensions;
using Netwolf.Transport.IRC;

using System.Text;
using System.Threading.RateLimiting;

namespace Netwolf.Transport.RateLimiting;

internal class RateLimiter : IRateLimiter
{
    /// <summary>
    /// Rate limiter where each permit/token represents a single command being sent
    /// </summary>
    private PartitionedRateLimiter<ICommand> CommandRateLimiter { get; init; }

    /// <summary>
    /// Rate limiter where each permit/token represents a single byte being sent
    /// </summary>
    private PartitionedRateLimiter<ICommand> ByteRateLimiter { get; init; }

    public RateLimiter(NetworkOptions options)
    {
        List<PartitionedRateLimiter<ICommand>> limiters = [];

        // Per-target limiters (PRIVMSG/NOTICE/TAGMSG)
        if (options.DefaultPerTargetLimiter.Enabled || options.PerTargetLimiter.Any(o => o.Value.Enabled))
        {
            limiters.Add(PartitionedRateLimiter.Create<ICommand, string>(cmd =>
            {
                var target = cmd.GetMessageTarget();
                if (target == null)
                {
                    return RateLimitPartition.GetNoLimiter(string.Empty);
                }

                var config = options.PerTargetLimiter.GetValueOrDefault(target, options.DefaultPerTargetLimiter);
                if (!config.Enabled)
                {
                    return RateLimitPartition.GetNoLimiter(string.Empty);
                }

                return RateLimitPartition.GetTokenBucketLimiter(target, _ => new()
                {
                    AutoReplenishment = false,
                    QueueLimit = options.RateLimiterMaxCommands,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    ReplenishmentPeriod = TimeSpan.FromMilliseconds(config.ReplenishmentRate),
                    TokensPerPeriod = config.ReplenishmentAmount,
                    TokenLimit = config.MaxTokens,
                });
            }));
        }

        // Per-command limiters
        if (options.PerCommandLimiter.Any(o => o.Value.Enabled))
        {
            limiters.Add(PartitionedRateLimiter.Create<ICommand, string>(cmd =>
            {
                string? key = null;
                string withArity = $"{cmd.Verb}`{cmd.Args.Count}";

                if (options.PerCommandLimiter.TryGetValue(withArity, out var config))
                {
                    key = withArity;
                }
                else if (options.PerCommandLimiter.TryGetValue(cmd.Verb, out config))
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
                    QueueLimit = options.RateLimiterMaxCommands,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    Window = TimeSpan.FromMilliseconds(config.Duration),
                    PermitLimit = config.Limit,
                    SegmentsPerWindow = config.Segments,
                });
            }));
        }

        // Global command limiter
        if (options.GlobalCommandLimiter.Enabled)
        {
            limiters.Add(PartitionedRateLimiter.Create<ICommand, string>(_ =>
            {
                return RateLimitPartition.GetTokenBucketLimiter(string.Empty, _ => new()
                {
                    AutoReplenishment = false,
                    QueueLimit = options.RateLimiterMaxCommands,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    ReplenishmentPeriod = TimeSpan.FromMilliseconds(options.GlobalCommandLimiter.ReplenishmentRate),
                    TokensPerPeriod = options.GlobalCommandLimiter.ReplenishmentAmount,
                    TokenLimit = options.GlobalCommandLimiter.MaxTokens,
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
        if (options.GlobalByteLimiter.Enabled)
        {
            ByteRateLimiter = PartitionedRateLimiter.Create<ICommand, string>(_ =>
            {
                return RateLimitPartition.GetSlidingWindowLimiter(string.Empty, _ => new()
                {
                    AutoReplenishment = false,
                    QueueLimit = options.RateLimiterMaxBytes,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    Window = TimeSpan.FromMilliseconds(options.GlobalByteLimiter.Duration),
                    PermitLimit = options.GlobalByteLimiter.Limit,
                    SegmentsPerWindow = options.GlobalByteLimiter.Segments,
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
    }

    public async ValueTask<RateLimitLease> AcquireAsync(ICommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var commandLease = await CommandRateLimiter.AcquireAsync(command, 1, cancellationToken).ConfigureAwait(false);
        
        // command.FullCommand omits the trailing CRLF, so add another 2 bytes to account for it
        var byteCount = Encoding.UTF8.GetByteCount(command.FullCommand) + 2;
        var byteLease = await ByteRateLimiter.AcquireAsync(command, byteCount, cancellationToken).ConfigureAwait(false);
        
        return new CommandLimiterLease(commandLease, byteLease);
    }

    private sealed class CommandLimiterLease : RateLimitLease
    {
        private bool _disposed = false;

        private readonly RateLimitLease _commandLease;
        private readonly RateLimitLease _byteLease;

        public override bool IsAcquired => _commandLease.IsAcquired && _byteLease.IsAcquired;

        public override IEnumerable<string> MetadataNames => _commandLease.MetadataNames.Concat(_byteLease.MetadataNames).Distinct();

        public CommandLimiterLease(RateLimitLease commandLease, RateLimitLease byteLease)
        {
            _commandLease = commandLease;
            _byteLease = byteLease;
        }

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            if (_commandLease.TryGetMetadata(metadataName, out metadata))
            {
                return true;
            }

            if (_byteLease.TryGetMetadata(metadataName, out metadata))
            {
                return true;
            }

            metadata = null;
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _commandLease.Dispose();
                    _byteLease.Dispose();
                }

                _disposed = true;
            }
        }
    }
}
