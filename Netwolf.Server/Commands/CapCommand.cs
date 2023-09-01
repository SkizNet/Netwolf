using Netwolf.Server.Capabilities;
using Netwolf.Server.Internal;
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

    private ICapabilityManager CapabilityManager { get; init; }

    public CapCommand(ICapabilityManager capabilityManager)
    {
        CapabilityManager = capabilityManager;
    }

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
            "END" => await HandleEnd(command, client, cancellationToken),
            _ => new NumericResponse(client, Numeric.ERR_INVALIDCAPCOMMAND, subCommand),
        };
    }

    private async Task<ICommandResponse> HandleLs(ICommand command, User client, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        int version = 301;
        var nick = client.Nickname ?? "*";

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

        // defer client registration if needed; this no-ops if the client is already registered
        client.AddRegistrationFlag(RegistrationFlags.PendingCapNegotation);

        // record the highest CAP version the client supports
        client.CapabilityVersion = Math.Max(client.CapabilityVersion, version);

        if (version >= 302)
        {
            _ = CapabilityManager.ApplyCapabilitySet(client, new[] { "cap-notify" }, Array.Empty<string>());
        }

        List<string> tokens = new();

        foreach (var cap in CapabilityManager.GetAllCapabilities())
        {
            if (version >= 302 && cap.Value != null)
            {
                tokens.Add($"{cap.Name}={cap.Value}");
            }
            else
            {
                tokens.Add(cap.Name);
            }
        }

        if (version == 301)
        {
            // no multiline support; prioritize listing standard (non-draft, non-vendor) caps
            tokens.Sort((x, y) =>
            {
                int xIsLowPriority = x.Contains('/') ? 1 : 0;
                int yIsLowPriority = y.Contains('/') ? 1 : 0;

                if ((xIsLowPriority ^ yIsLowPriority) != 0)
                {
                    return xIsLowPriority.CompareTo(yIsLowPriority);
                }

                return x.CompareTo(y);
            });

            // account for the prefix ":servername CAP nick LS :" and the CRLF suffix
            int consumedBytes = client.Network.ServerName.Length + nick.Length + 13;
            var param = String.Join(' ', tokens.TakeWhile(t => (consumedBytes += t.Length) <= client.MaxBytesPerLine));

            return new CommandResponse(client, null, "CAP", nick, "LS", param);
        }
        else
        {
            // account for the prefix ":servername CAP nick LS * :" and the CRLF suffix
            int maxBytes = client.MaxBytesPerLine - (client.Network.ServerName.Length + nick.Length + 15);
            int consumedBytes = 0;
            List<string> param = new();
            var response = new MultiResponse();

            foreach (var token in tokens)
            {
                if (consumedBytes + token.Length > maxBytes)
                {
                    response.Add(new CommandResponse(client, null, "CAP", nick, "LS", "*", String.Join(' ', param)));
                    consumedBytes = 0;
                    param.Clear();
                }

                param.Add(token);
                consumedBytes += token.Length;
            }

            response.Add(new CommandResponse(client, null, "CAP", nick, "LS", String.Join(' ', param)));
            return response;
        }
    }
}
