using Netwolf.Transport.IRC;

namespace Netwolf.Server.Commands;

public interface ICommandHandler
{
    string Command { get; }

    string? Privilege { get; }

    bool HasChannel { get; }

    bool AllowBeforeRegistration { get; }

    Task<ICommandResponse> ExecuteAsync(ICommand command, User client, Channel? channel, CancellationToken cancellationToken);
}
