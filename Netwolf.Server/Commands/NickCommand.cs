using Netwolf.Server.Internal;
using Netwolf.Transport.Client;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Netwolf.Server.Commands;

public partial class NickCommand : ICommandHandler
{
    // RFC 2812 nickname validation
    [GeneratedRegex("[a-zA-Z[\\]\\\\`_^{}|][a-zA-Z0-9[\\]\\\\`_^{}|-]*")]
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

        // TODO: Make nick length configurable
        if (nick.Length > 20 || !ValidNickRegex().IsMatch(nick))
        {
            return Task.FromResult<ICommandResponse>(new NumericResponse(client, Numeric.ERR_ERRONEUSNICKNAME, nick));
        }

        if (client.Network.Clients.Any(o => o.Value.Nickname == nick))
        {
            return Task.FromResult<ICommandResponse>(new NumericResponse(client, Numeric.ERR_NICKNAMEINUSE, nick));
        }

        string? source = client.Registered ? client.Hostmask : null;
        var response = new CommandResponse(client, source, "NICK", nick);

        client.Nickname = nick;

        if (client.RegistrationFlags.HasFlag(RegistrationFlags.PendingNick))
        {
            if (!client.MaybeDoImplicitPassCommand(RegistrationFlags.PendingNick))
            {
                // TODO: move string to a resource file for l10n
                return Task.FromResult<ICommandResponse>(new ErrorResponse(client, "You do not have access to this network (missing password?)."));
            }

            var batch = client.ClearRegistrationFlag(RegistrationFlags.PendingNick);
            if (batch != null)
            {
                return Task.FromResult<ICommandResponse>(batch);
            }
        }

        return Task.FromResult<ICommandResponse>(response);
    }
}
