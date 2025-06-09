using Netwolf.Server.Users;
using Netwolf.Transport.IRC;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Commands;

public class MotdCommand : ServerCommandHandler
{
    public override string Command => "MOTD";

    public override Task<ICommandResponse> ExecuteAsync(ICommand command, IContext sender, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<ICommandResponse>(ExecuteInternal(((ServerContext)sender).User!));
    }

    internal static MultiResponse ExecuteInternal(User client)
    {
        // We do not implement or support the target parameter for this command,
        // as Netwolf does expose the individual servers comprising the network

        // TODO: support showing a real MOTD if one is set in network config
        var batch = new MultiResponse();
        batch.AddNumeric(client, Numeric.ERR_NOMOTD);
        return batch;
    }
}
