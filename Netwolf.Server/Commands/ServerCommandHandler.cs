﻿using Netwolf.PluginFramework.Commands;
using Netwolf.PluginFramework.Context;

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

    public abstract Task<ICommandResponse> ExecuteAsync(ICommand command, IContext sender, CancellationToken cancellationToken);

    string? ICommandHandler.Privilege => Privilege;
}
