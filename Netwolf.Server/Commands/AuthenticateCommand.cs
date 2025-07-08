using Microsoft.Extensions.Options;

using Netwolf.Server.Capabilities;
using Netwolf.Server.Sasl;
using Netwolf.Server.Users;
using Netwolf.Transport.Commands;

namespace Netwolf.Server.Commands;

public class AuthenticateCommand : ServerCommandHandler
{
    public override bool AllowBeforeRegistration => true;

    public override string Command => "AUTHENTICATE";

    private ISaslLookup SaslLookup { get; init; }

    private IOptionsSnapshot<ServerOptions> Options { get; init; }

    public AuthenticateCommand(ISaslLookup saslLookup, IOptionsSnapshot<ServerOptions> options)
    {
        SaslLookup = saslLookup;
        Options = options;
    }

    public override async Task<ICommandResponse> ExecuteAsync(ICommand command, ServerContext sender, CancellationToken cancellationToken)
    {
        var client = sender.User ?? throw new InvalidOperationException("Context is missing a user");
        if (!client.HasCapability<SaslCapability>())
        {
            return new NumericResponse(client, Numeric.ERR_UNKNOWNCOMMAND, "You must request the sasl capability before using this command", ["AUTHENTICATE"]);
        }
        else if (command.Args.Count == 0)
        {
            return new NumericResponse(client, Numeric.ERR_NEEDMOREPARAMS, Command);
        }

        // for now, don't support reauthentication
        if (client.IsAuthenticated)
        {
            return new NumericResponse(client, Numeric.ERR_SASLALREADY);
        }

        // are they aborting?
        if (command.Args[0] == "*")
        {
            return new NumericResponse(client, Numeric.ERR_SASLABORTED);
        }

        if (command.Args[0].Length > 400)
        {
            return new NumericResponse(client, Numeric.ERR_SASLTOOLONG);
        }

        // figure out what we're doing based on expected next state for this client
        if (client.SaslState == null || client.SaslState.Completed)
        {
            // expecting a mechanism name
            // RFC 4616 for PLAIN
            // RFC 4422 for EXTERNAL
            // RFC 7677 for SCRAM-*
            // No RFC for ECDSA-NIST256P-CHALLENGE, follow atheme as reference implementation
            // RFC 7628 for OAUTHBEARER
            // RFC 2444 for OTP
            // RFC 6616 for OPENID20

            if (!SaslLookup.TryGet(command.Args[0].ToUpperInvariant(), out var selectedMech))
            {
                return new NumericResponse(client, Numeric.RPL_SASLMECHS, string.Join(',', Options.Value.EnabledSaslMechanisms));
            }

            client.SaslState = selectedMech.InitializeForUser(client);
        }

        var response = await client.SaslState.ProcessClientCommandAsync(command, cancellationToken);
        if (client.SaslState.Errored)
        {
            client.SaslFailures++;
            // TODO: make configurable?
            if (client.SaslFailures >= 3)
            {
                response = new MultiResponse()
                {
                    response,
                    new ErrorResponse(client, "Too many SASL failures")
                };
            }
        }

        return response;
    }
}
