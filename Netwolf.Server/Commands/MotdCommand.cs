using Netwolf.Transport.Client;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Commands;

public class MotdCommand : ICommandHandler
{
    public string Command => "MOTD";

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
        // We do not implement or support the target parameter for this command,
        // as Netwolf does expose the individual servers comprising the network

        // TODO: support showing a real MOTD if one is set in network config
        var batch = new MultiResponse();
        batch.AddNumeric(client, Numeric.ERR_NOMOTD);
        return batch;
    }
}
