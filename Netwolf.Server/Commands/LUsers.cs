using Netwolf.Transport.Client;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Commands;

public class LUsers : ICommandHandler
{
    public string Command => "LUSERS";

    public string? Privilege => null;

    public bool HasChannel => false;

    public bool AllowBeforeRegistration => false;

    public Task<ICommandResponse> ExecuteAsync(ICommand command, User client, Channel? channel, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<ICommandResponse>(ExecuteInternal(client));
    }

    internal static MultiResponse ExecuteInternal(User client)
    {
        var batch = new MultiResponse();

        batch.AddNumeric(client, Numeric.RPL_LUSERCLIENT);
        batch.AddNumeric(client, Numeric.RPL_LUSEROP);

        if (client.HasPrivilege("oper:lusers:unknown"))
        {
            batch.AddNumeric(client, Numeric.RPL_LUSERUNKNOWN);
        }

        batch.AddNumeric(client, Numeric.RPL_LUSERCHANNELS);
        batch.AddNumeric(client, Numeric.RPL_LUSERME);

        // Netwolf has no concept of local vs global users, so don't give RPL_LOCALUSERS
        batch.AddNumeric(client, Numeric.RPL_GLOBALUSERS);

        return batch;
    }
}
