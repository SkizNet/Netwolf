using Netwolf.Server.Internal;
using Netwolf.Transport.Client;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Netwolf.Server.Commands;

public partial class Nick : ICommandHandler
{
    // RFC 2812 nickname validation
    [GeneratedRegex("[a-zA-Z[\\]\\\\`_^{}|][a-zA-Z0-9[\\]\\\\`_^{}|-]{0,15}")]
    private static partial Regex ValidNickRegex();

    public string Command => "NICK";

    public string? Privilege => null;

    public bool HasChannel => false;

    public bool AllowBeforeRegistration => true;

    public Task<ICommandResponse> ExecuteAsync(ICommand command, User client, Channel? channel, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (command.Args.Count == 0 || command.Args[0].Length == 0)
        {
            return Task.FromResult<ICommandResponse>(new NumericResponse(client, Numeric.ERR_NONICKNAMEGIVEN));
        }

        string nick = command.Args[0];

        if (!ValidNickRegex().IsMatch(nick))
        {
            return Task.FromResult<ICommandResponse>(new NumericResponse(client, Numeric.ERR_ERRONEUSNICKNAME, nick));
        }

        if (client.Network.Clients.Any(o => o.Value.Nickname == nick))
        {
            return Task.FromResult<ICommandResponse>(new NumericResponse(client, Numeric.ERR_NICKNAMEINUSE, nick));
        }

        client.Nickname = nick;

        if (client.RegistrationFlags.HasFlag(RegistrationFlags.PendingNick))
        {
            if (client.RegistrationFlags.HasFlag(RegistrationFlags.PendingPass) && !client.AttachConnectionConfig(null))
            {
                // TODO: move string to a resource file for l10n
                return Task.FromResult<ICommandResponse>(new ErrorResponse(client, "You do not have access to this network (missing password?)."));
            }

            client.RegistrationFlags ^= RegistrationFlags.PendingNick;
        }

        return Task.FromResult<ICommandResponse>(new CommandResponse(client, "NICK", nick));
    }
}
