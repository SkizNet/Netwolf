using Netwolf.Server.Capabilities;
using Netwolf.Transport.IRC;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Commands;

public class CapCommand : ICommandHandler
{
    public string Command => "CAP";

    public bool HasChannel => false;

    public async Task<ICommandResponse> ExecuteAsync(ICommand command, User client, Channel? channel, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (command.Args.Count == 0)
        {
            return new NumericResponse(client, Numeric.ERR_NEEDMOREPARAMS);
        }

        // determine sub-command
        var subCommand = command.Args[0].ToUpperInvariant();
        return subCommand switch
        {
            "LS" => await HandleLs(command, client, cancellationToken),
            "LIST" => await HandleList(command, client, cancellationToken),
            "REQ" => await HandleReq(command, client, cancellationToken),
            _ => new NumericResponse(client, Numeric.ERR_INVALIDCAPCOMMAND, subCommand),
        };
    }

    private async Task<ICommandResponse> HandleLs(ICommand command, User client, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        int version = 301;

        if (command.Args.Count > 1)
        {
            // we have a version
            if (!Int32.TryParse(command.Args[1], out version))
            {
                // client gave us garbage
                return new NumericResponse(client, Numeric.ERR_INVALIDCAPCOMMAND, "LS");
            }

            // 301 is the lowest version that could be supported, so normalize anything lower to that
            // (the fact the client knows that CAP exists means it supports version 301)
            version = Math.Max(version, 301);
        }

        // record the highest CAP version the client supports
        client.CapabilityVersion = Math.Max(client.CapabilityVersion, version);

        if (version >= 302)
        {
            // TODO: use a capability manager service (so we can do things like dependencies, etc.)
            client.Capabilities.Add(new CapNotifyCapability());
        }
    }
}
