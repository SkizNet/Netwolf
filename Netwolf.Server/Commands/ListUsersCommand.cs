using Netwolf.Server.Users;
using Netwolf.Transport.Commands;
using Netwolf.Transport.Context;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Commands;

public class ListUsersCommand : ServerCommandHandler
{
    public override string Command => "LUSERS";

    public override Task<ICommandResponse> ExecuteAsync(ICommand command, ServerContext sender, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<ICommandResponse>(ExecuteInternal(sender.User!));
    }

    internal static MultiResponse ExecuteInternal(User client)
    {
        var batch = new MultiResponse();

        batch.AddNumeric(client, Numeric.RPL_LUSERCLIENT);
        batch.AddNumeric(client, Numeric.RPL_LUSEROP, client.Network.Clients.Count(c => c.Value.HasPrivilege("oper:general")).ToString());

        if (client.HasPrivilege("oper:lusers:unknown"))
        {
            batch.AddNumeric(client, Numeric.RPL_LUSERUNKNOWN, client.Network.PendingCount.ToString());
        }

        batch.AddNumeric(client, Numeric.RPL_LUSERCHANNELS, client.Network.ChannelCount.ToString());
        batch.AddNumeric(client, Numeric.RPL_LUSERME);

        // Netwolf has no concept of local vs global users, so we give the same numbers for both
        batch.AddNumeric(client, Numeric.RPL_LOCALUSERS, client.Network.UserCount.ToString(), client.Network.MaxUserCount.ToString());
        batch.AddNumeric(client, Numeric.RPL_GLOBALUSERS, client.Network.UserCount.ToString(), client.Network.MaxUserCount.ToString());

        return batch;
    }
}
