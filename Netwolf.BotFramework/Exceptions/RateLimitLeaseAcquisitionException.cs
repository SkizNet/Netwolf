using Netwolf.PluginFramework.Commands;

namespace Netwolf.BotFramework.Exceptions;

public class RateLimitLeaseAcquisitionException : Exception
{
    public ICommand Command { get; init; }

    public Dictionary<string, object?> LeaseMetadata { get; init; }

    public RateLimitLeaseAcquisitionException(ICommand command, Dictionary<string, object?> leaseMetadata)
        : base("Unable to acquire rate limiter lease")
    {
        Command = command;
        LeaseMetadata = leaseMetadata;
    }
}
