using Netwolf.Transport.Client;

namespace Netwolf.Server.Commands;

public interface ICommandDispatcher
{
    Task<ICommandResponse> DispatchAsync(ICommand command, User client);
}
