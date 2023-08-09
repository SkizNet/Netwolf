using Netwolf.Transport.IRC;

namespace Netwolf.Server.Commands;

public interface ICommandDispatcher
{
    Task<ICommandResponse> DispatchAsync(ICommand command, User client, CancellationToken cancellationToken);
}
