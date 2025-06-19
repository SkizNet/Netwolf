using Microsoft.Extensions.Options;

using Netwolf.Server.Capabilities;
using Netwolf.Server.Users;
using Netwolf.Transport.Commands;
using Netwolf.Transport.Context;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Commands;

public class AuthenticateCommand : ServerCommandHandler
{
    private enum SaslMech
    {
        // RFC 4616
        Plain,
        // RFC 4422
        External,
        // RFC 7677
        ScramSha1,
        ScramSha1Plus,
        ScramSha256,
        ScramSha256Plus,
        ScramSha512,
        ScramSha512Plus,
        // No RFC, follow atheme as reference implementation
        EcdsaNist256pChallenge,
        // RFC 7628
        OAuthBearer,
        // RFC 2444
        Otp,
        // RFC 6616
        OpenId20,
    }

    private record SaslState(SaslMech SelectedMech, int Step, MemoryStream ClientBuffer, MemoryStream ServerBuffer);

    public override bool AllowBeforeRegistration => true;

    public override string Command => "AUTHENTICATE";

    private readonly Dictionary<User, SaslState> _state = [];

    private IOptionsSnapshot<ServerOptions> Options { get; init; }

    public AuthenticateCommand(IOptionsSnapshot<ServerOptions> options)
    {
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
        if (client.Account != null)
        {
            return new NumericResponse(client, Numeric.ERR_SASLALREADY);
        }

        // are they aborting?
        if (command.Args[0] == "*")
        {
            return new NumericResponse(client, Numeric.ERR_SASLABORTED);
        }

        // figure out what we're doing based on expected next state for this client
        if (!_state.TryGetValue(client, out var state))
        {
            // expecting a mechanism name
            var clientMech = command.Args[0].ToUpperInvariant();
            if (!Options.Value.EnabledSaslMechanisms.Contains(clientMech))
            {
                return new NumericResponse(client, Numeric.RPL_SASLMECHS, string.Join(',', Options.Value.EnabledSaslMechanisms));
            }

            SaslMech selectedMech = clientMech switch
            {
                "PLAIN" => SaslMech.Plain,
                "EXTERNAL" => SaslMech.External,
                "SCRAM-SHA-1" => SaslMech.ScramSha1,
                "SCRAM-SHA-1-PLUS" => SaslMech.ScramSha1Plus,
                "SCRAM-SHA-256" => SaslMech.ScramSha256,
                "SCRAM-SHA-256-PLUS" => SaslMech.ScramSha256Plus,
                "SCRAM-SHA-512" => SaslMech.ScramSha512,
                "SCRAM-SHA-512-PLUS" => SaslMech.ScramSha512Plus,
                "ECDSA-NIST256P-CHALLENGE" => SaslMech.EcdsaNist256pChallenge,
                "OAUTHBEARER" => SaslMech.OAuthBearer,
                "OTP" => SaslMech.Otp,
                "OPENID20" => SaslMech.OpenId20,
                _ => throw new ArgumentException($"Unknown SASL mechanism {clientMech}"),
            };
        }

        throw new NotImplementedException();
    }

    private async Task TimeoutAuth(User client)
    {

    }
}
