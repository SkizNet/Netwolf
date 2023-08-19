using Netwolf.Transport.IRC;

namespace Netwolf.Server.Commands;

public interface ICommandHandler
{
    string Command { get; }

    string? Privilege => null;

    bool HasChannel { get; }

    bool AllowBeforeRegistration => false;

    Task<ICommandResponse> ExecuteAsync(ICommand command, User client, Channel? channel, CancellationToken cancellationToken);
}
