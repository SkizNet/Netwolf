using Netwolf.Transport.Client;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Netwolf.Server.Commands;

public class Nick : ICommandHandler
{
    public string Command => "NICK";

    public string? Privilege => null;

    public bool HasChannel => false;

    public Task<ICommandResponse> ExecuteAsync(ICommand command, User client, Channel? channel, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (command.Args.Count == 0 || command.Args[0].Length == 0)
        {
            return Task.FromResult<ICommandResponse>(new NumericResponse(client, Numeric.ERR_NONICKNAMEGIVEN));
        }

        string nick = command.Args[0];

        // RFC 2812 nickname validation
        if (!Regex.IsMatch(nick, @"[a-zA-Z[\]\\`_^{}|][a-zA-Z0-9[\]\\`_^{}|-]{0,15}"))
        {
            return Task.FromResult<ICommandResponse>(new NumericResponse(client, Numeric.ERR_ERRONEUSNICKNAME, nick));
        }

        if (client.Network.Clients.Any(o => o.Value.Nickname == nick))
        {
            return Task.FromResult<ICommandResponse>(new NumericResponse(client, Numeric.ERR_NICKNAMEINUSE, nick));
        }

        client.Nickname = nick;

        if (!client.Registered)
        {
            CheckRegistrationComplete(client);
        }
        else
        {
            Reply(client, null, null, "NICK", nick);
        }
    }
}
