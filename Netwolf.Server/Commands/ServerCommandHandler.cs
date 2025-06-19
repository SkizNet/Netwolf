using Netwolf.PluginFramework.Commands;
using Netwolf.Server.Users;
using Netwolf.Transport.Commands;
using Netwolf.Transport.Context;

namespace Netwolf.Server.Commands;

/// <summary>
/// Abstract base class to wrap a server command handler, to avoid pitfalls when
/// attempting to override default interface implementations that would silently not work
/// because default interface implementations don't support implicit overrides.
/// </summary>
public abstract class ServerCommandHandler : ICommandHandler<ICommandResponse>
{
    public virtual bool AllowBeforeRegistration => false;

    public virtual bool HasChannel => false;

    public abstract string Command { get; }

    public virtual string? Privilege => null;

    Task<ICommandResponse> ICommandHandler<ICommandResponse>.ExecuteAsync(ICommand command, IContext sender, CancellationToken cancellationToken)
    {
        return ExecuteAsync(command, (ServerContext)sender, cancellationToken);
    }

    public abstract Task<ICommandResponse> ExecuteAsync(ICommand command, ServerContext sender, CancellationToken cancellationToken);

    string? ICommandHandler.Privilege => Privilege;
}
