using Netwolf.Server.Internal;
using Netwolf.Server.Users;
using Netwolf.Transport.Commands;
using Netwolf.Transport.Context;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Netwolf.Server.Commands;

public partial class NickCommand : ServerCommandHandler
{
    // RFC 2812 nickname validation
    [GeneratedRegex("[a-zA-Z[\\]\\\\`_^{}|][a-zA-Z0-9[\\]\\\\`_^{}|-]*")]
    private static partial Regex ValidNickRegex();

    public override string Command => "NICK";

    public override bool AllowBeforeRegistration => true;

    public override async Task<ICommandResponse> ExecuteAsync(ICommand command, IContext sender, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var client = ((ServerContext)sender).User!;

        if (command.Args.Count == 0)
        {
            return new NumericResponse(client, Numeric.ERR_NONICKNAMEGIVEN);
        }

        string nick = command.Args[0];

        // TODO: Make nick length configurable
        if (nick.Length > 20 || !ValidNickRegex().IsMatch(nick))
        {
            return new NumericResponse(client, Numeric.ERR_ERRONEUSNICKNAME, nick);
        }

        if (client.Network.Clients.ContainsKey(nick.ToUpperInvariant()))
        {
            return new NumericResponse(client, Numeric.ERR_NICKNAMEINUSE, nick);
        }

        string? source = client.Registered ? client.Hostmask : null;
        ICommandResponse response = new CommandResponse(client, source, "NICK", nick);

        client.Nickname = nick;

        if (client.RegistrationFlags.HasFlag(RegistrationFlags.PendingNick))
        {
            if (!await client.MaybeDoImplicitPassCommand(RegistrationFlags.PendingNick))
            {
                // TODO: move string to a resource file for l10n
                return new ErrorResponse(client, "You do not have access to this network (missing password?).");
            }

            response = client.ClearRegistrationFlag(RegistrationFlags.PendingNick);
        }
        else
        {
            // TODO: notify the client and everyone sharing a channel with the client of the nick change
        }

        return response;
    }
}
