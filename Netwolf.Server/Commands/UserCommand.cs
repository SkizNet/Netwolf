using Netwolf.Server.Internal;
using Netwolf.Transport.Client;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Commands;

public class UserCommand : ICommandHandler
{
    public string Command => "USER";

    public string? Privilege => null;

    public bool HasChannel => false;

    public bool AllowBeforeRegistration => true;

    public Task<ICommandResponse> ExecuteAsync(ICommand command, User client, Channel? channel, CancellationToken cancellationToken)
    {
        if (client.Registered)
        {
            return Task.FromResult<ICommandResponse>(new NumericResponse(client, Numeric.ERR_ALREADYREGISTERED));
        }

        if (command.Args.Count != 4 || command.Args[0].Length == 0)
        {
            return Task.FromResult<ICommandResponse>(new NumericResponse(client, Numeric.ERR_NEEDMOREPARAMS, command.Verb));
        }

        // TODO: Make configurable (or at least use some const defined in client.Network rather than hardcoding the ident/gecos lengths here too)
        if (client.FullIdent == null)
        {
            client.Ident = command.Args[0][..11];
        }

        client.UserParam1 = command.Args[1];
        client.UserParam2 = command.Args[2];
        client.RealName = command.Args[3][..150];

        if (!client.MaybeDoImplicitPassCommand(RegistrationFlags.PendingUser))
        {
            // TODO: move string to a resource file for l10n
            return Task.FromResult<ICommandResponse>(new ErrorResponse(client, "You do not have access to this network (missing password?)."));
        }

        var batch = client.ClearRegistrationFlag(RegistrationFlags.PendingUser);
        if (batch != null)
        {
            return Task.FromResult<ICommandResponse>(batch);
        }

        return Task.FromResult<ICommandResponse>(new EmptyResponse());
    }
}
