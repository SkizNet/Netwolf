using Netwolf.PluginFramework.Commands;
using Netwolf.PluginFramework.Context;
using Netwolf.Server.Capabilities;
using Netwolf.Server.Internal;
using Netwolf.Server.Users;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Commands;

public class CapCommand : ServerCommandHandler
{
    public override string Command => "CAP";

    public override bool AllowBeforeRegistration => true;

    private ICapabilityManager CapabilityManager { get; init; }

    public CapCommand(ICapabilityManager capabilityManager)
    {
        CapabilityManager = capabilityManager;
    }

    public override async Task<ICommandResponse> ExecuteAsync(ICommand command, IContext sender, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var client = ((ServerContext)sender).User!;

        if (command.Args.Count == 0)
        {
            return new NumericResponse(client, Numeric.ERR_NEEDMOREPARAMS);
        }

        // determine sub-command
        var subCommand = command.Args[0].ToUpperInvariant();
        return subCommand switch
        {
            "LS" => await HandleLs(command, client, cancellationToken),
            "LIST" => await HandleList(client, cancellationToken),
            "REQ" => await HandleReq(command, client, cancellationToken),
            "END" => await client.MaybeDoImplicitPassCommand(RegistrationFlags.PendingCapNegotation)
                ? client.ClearRegistrationFlag(RegistrationFlags.PendingCapNegotation)
                : new ErrorResponse(client, "You do not have access to this network (missing password?)."),
            _ => new NumericResponse(client, Numeric.ERR_INVALIDCAPCOMMAND, subCommand),
        };
    }

    private static ICommandResponse SplitCapabilityList(User client, string subcommand, IEnumerable<ICapability> capabilities)
    {
        List<string> tokens = new();

        // don't show values if the CAP version is 301 or if we're replying to CAP LIST
        bool showValue = client.CapabilityVersion > 301 && subcommand == "LS";

        // this might be pre-registration, so client.Nickname might be null; use * if that's the case
        string nick = client.Nickname ?? "*";

        foreach (var cap in capabilities)
        {
            if (showValue && cap.Value != null)
            {
                tokens.Add($"{cap.Name}={cap.Value}");
            }
            else
            {
                tokens.Add(cap.Name);
            }
        }

        if (client.CapabilityVersion == 301)
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

            // account for the prefix ":servername CAP nick subcommand :" and the CRLF suffix
            int consumedBytes = client.Network.ServerName.Length + nick.Length + subcommand.Length + 11;
            var param = String.Join(' ', tokens.TakeWhile(t => (consumedBytes += t.Length) <= client.MaxBytesPerLine));

            return new CommandResponse(client, null, "CAP", nick, subcommand, param);
        }
        else
        {
            // account for the prefix ":servername CAP nick subcommand * :" and the CRLF suffix
            int maxBytes = client.MaxBytesPerLine - (client.Network.ServerName.Length + nick.Length + subcommand.Length + 13);
            int consumedBytes = 0;
            List<string> param = new();
            var response = new MultiResponse();

            foreach (var token in tokens)
            {
                if (consumedBytes + token.Length > maxBytes)
                {
                    response.Add(new CommandResponse(client, null, "CAP", nick, subcommand, "*", String.Join(' ', param)));
                    consumedBytes = 0;
                    param.Clear();
                }

                param.Add(token);
                consumedBytes += token.Length;
            }

            response.Add(new CommandResponse(client, null, "CAP", nick, subcommand, String.Join(' ', param)));
            return response;
        }
    }

    private Task<ICommandResponse> HandleLs(ICommand command, User client, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        int version = 301;

        if (command.Args.Count > 1)
        {
            // we have a version
            if (!Int32.TryParse(command.Args[1], out version))
            {
                // client gave us garbage
                return Task.FromResult<ICommandResponse>(new NumericResponse(client, Numeric.ERR_INVALIDCAPCOMMAND, "LS"));
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

        return Task.FromResult(SplitCapabilityList(client, "LS", CapabilityManager.GetAllCapabilities()));
    }

    private Task<ICommandResponse> HandleList(User client, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(SplitCapabilityList(client, "LIST", client.Capabilities));
    }

    private Task<ICommandResponse> HandleReq(ICommand command, User client, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (command.Args.Count != 2 || String.IsNullOrWhiteSpace(command.Args[1]))
        {
            // nothing requested
            return Task.FromResult<ICommandResponse>(new NumericResponse(client, Numeric.ERR_INVALIDCAPCOMMAND, "REQ"));
        }

        HashSet<string> add = new();
        HashSet<string> remove = new();

        foreach (var token in command.Args[1].Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (token[0] == '-')
            {
                add.Remove(token[1..]);
                remove.Add(token[1..]);
            }
            else
            {
                add.Add(token);
                remove.Remove(token);
            }
        }

        // if we were given a long REQ, we may need multiple lines for the ACK or NAK
        string subcommand = CapabilityManager.ApplyCapabilitySet(client, add, remove) ? "ACK" : "NAK";
        var tokens = add.Concat(remove.Select(x => $"-{x}"));
        string nick = client.Nickname ?? "*";

        // account for ":servername CAP nick ACK|NAK :" prefix and CRLF suffix
        int maxBytes = client.MaxBytesPerLine - (client.Network.ServerName.Length + nick.Length + 14);
        int consumedBytes = 0;
        List<string> param = new();
        var response = new MultiResponse();

        foreach (var token in tokens)
        {
            if (consumedBytes + token.Length > maxBytes)
            {
                // unlike multiline LS/LIST, the spec doesn't tell us to add an extra * param for multiline ACK/NAK
                response.Add(new CommandResponse(client, null, "CAP", nick, subcommand, String.Join(' ', param)));
                consumedBytes = 0;
                param.Clear();
            }

            param.Add(token);
            consumedBytes += token.Length;
        }

        response.Add(new CommandResponse(client, null, "CAP", nick, subcommand, String.Join(' ', param)));

        return Task.FromResult<ICommandResponse>(response);
    }
}
