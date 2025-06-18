using Netwolf.Server.Extensions.Internal;
using Netwolf.Server.Internal;
using Netwolf.Server.Users;
using Netwolf.Transport.Commands;
using Netwolf.Transport.Context;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Commands;

public class UserCommand : ServerCommandHandler
{
    public override string Command => "USER";

    public override bool AllowBeforeRegistration => true;

    public override async Task<ICommandResponse> ExecuteAsync(ICommand command, IContext sender, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var client = ((ServerContext)sender).User!;

        if (client.Registered)
        {
            return new NumericResponse(client, Numeric.ERR_ALREADYREGISTERED);
        }

        if (command.Args.Count < 4)
        {
            return new NumericResponse(client, Numeric.ERR_NEEDMOREPARAMS, Command);
        }

        // TODO: Make configurable (or at least use some const defined in client.Network rather than hardcoding the ident/gecos lengths here too)
        // Also update User.LookUpIdent with the new constant when that exists
        if (client.FullIdent == null)
        {
            client.Ident = $"{client.IdentPrefix}{command.Args[0]}".Truncate(11);
        }

        client.UserParam1 = command.Args[1];
        client.UserParam2 = command.Args[2];
        client.RealName = command.Args[3].Truncate(150);

        if (!await client.MaybeDoImplicitPassCommand(RegistrationFlags.PendingUser))
        {
            // TODO: move string to a resource file for l10n
            return new ErrorResponse(client, "You do not have access to this network (missing password?).");
        }

        return client.ClearRegistrationFlag(RegistrationFlags.PendingUser);
    }
}
