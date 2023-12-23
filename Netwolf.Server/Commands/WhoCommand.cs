using Microsoft.Extensions.Options;

using Netwolf.Server.Capabilities.Vendor;
using Netwolf.Server.ChannelModes;
using Netwolf.Server.Internal;
using Netwolf.Server.ISupport;
using Netwolf.Transport.IRC;

using System.Net;
using System.Text;

namespace Netwolf.Server.Commands;

public class WhoCommand : ICommandHandler, IISupportTokenProvider
{
    public string Command => "WHO";

    public string? Privilege => null;

    // We optionally take a channel but don't require one, so that is handled in the Execute logic
    public bool HasChannel => false;

    public bool AllowBeforeRegistration => false;

    private bool WhoxEnabled { get; init; }

    private static string[] ResolveAttributes(User client, User target, IEnumerable<char> attributes)
    {
        List<string> values = new();
        foreach (var attribute in attributes)
        {
            values.Add(attribute switch
            {
                'n' => target.Nickname,
                'u' => target.Ident,
                'h' => target.DisplayHost,
                'r' => target.RealName,
                'a' => target.Account ?? "0",
                'i' => (target.VirtualHost == null || client.HasPrivilege("oper:auspex:user")) ? target.RealIP.ToString() : "255.255.255.255",
                _ => throw new InvalidOperationException($"Unknown WHO search flag {attribute}")
            });

            if (attribute == 'h' && target.VirtualHost != null && client.HasPrivilege("oper:auspex:user"))
            {
                values.Add(target.RealHost);
            }
        }

        return values.ToArray();
    }

    private static bool ResolveCidr(User client, User target, IPNetwork filter)
    {
        if (target.VirtualHost == null || client.HasPrivilege("oper:auspex:user"))
        {
            return filter.Contains(target.RealIP);
        }

        return false;
    }

    public WhoCommand(IOptionsSnapshot<ServerOptions> options)
    {
        WhoxEnabled = options.Value.EnabledFeatures.Contains("WHOX");
    }

    public Task<ICommandResponse> ExecuteAsync(ICommand command, User client, Channel? channel, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (command.Args.Count == 0)
        {
            return Task.FromResult<ICommandResponse>(new NumericResponse(client, Numeric.ERR_NEEDMOREPARAMS, Command));
        }

        // save a bit of horizontal space later on
        var network = client.Network;
        var mask = command.Args[0];
        var lookupKey = mask.ToUpperInvariant();
        bool operspy = mask[0] == '!' && client.HasPrivilege("oper:spy:who");

        if (operspy)
        {
            mask = mask[1..];
            lookupKey = lookupKey[1..];
        }

        var lookupChan = network.Channels.GetValueOrDefault(lookupKey);
        bool operOnly = false;
        string filter = "nuhr";
        string? whox = null;
        string? token = null;

        // process the 2nd parameter into (filter, whox, token)
        if (command.Args.Count > 1)
        {
            var bits = command.Args[1].Split('%', 2);
            if (bits[0].Contains('o'))
            {
                operOnly = true;
                bits[0] = bits[0].Replace("o", null);
            }

            if (bits[0] != String.Empty)
            {
                filter = bits[0];
            }

            if (bits.Length > 1)
            {
                bits = bits[1].Split(',', 2);
                whox = bits[0];

                if (whox.Contains('t'))
                {
                    if (bits.Length < 2 || bits[1].Length == 0 || bits[1].Length > 3 || bits[1].Any(c => !Char.IsAsciiDigit(c)))
                    {
                        token = "0";
                    }
                    else
                    {
                        token = bits[1];
                    }
                }
            }
        }

        // if WHOX isn't enabled, clear it out
        if (!WhoxEnabled)
        {
            whox = null;
        }

        IEnumerable<User> targets = network.Clients.Values;

        // if the first mask is a channel, use it even if a second mask is provided
        if (network.ChannelTypes.Contains(mask[0]))
        {
            targets = lookupChan?.Members.Keys ?? Array.Empty<User>();
            // if we don't have a second mask, grab everyone in the channel
            mask = "*";
        }

        // if we were given a second mask, use it
        if (command.Args.Count > 2)
        {
            mask = command.Args[2];
        }
        // if we weren't given a second mask, check if the first one is a nickname
        else if (network.Clients.ContainsKey(lookupKey))
        {
            targets = new User[] { network.Clients[lookupKey] };
        }

        // if the mask isn't 0 or *, apply it
        if (mask != "*" && mask != "0")
        {
            if (filter.Contains('i') && mask.Contains('/') && IPNetwork.TryParse(filter, out var n))
            {
                targets = targets.Where(u => Glob.For(mask).MatchAny(ResolveAttributes(client, u, filter)) || ResolveCidr(client, u, n));
            }
            else
            {
                targets = targets.Where(u => Glob.For(mask).MatchAny(ResolveAttributes(client, u, filter)));
            }
        }

        // restrict the list based on the visibility of target users
        if (!operspy)
        {
            // secret channel that we aren't on?
            if (lookupChan != null && lookupChan.HasMode<SecretChannelMode>() && !lookupChan.Members.ContainsKey(client))
            {
                targets = Array.Empty<User>();
            }
            else
            {
                targets = targets.Where(u => !u.Invisible || u.Channels.Overlaps(client.Channels));
            }
        }

        // do we only want opers?
        if (operOnly)
        {
            targets = targets.Where(u => u.HasPrivilege("oper:general"));
        }

        // get the channel we return for normal WHO results or WHOX with %c specified        
        string channelResult = lookupChan?.Name ?? client.Channels.FirstOrDefault()?.Name ?? "*";
        Numeric numeric = whox == null ? Numeric.RPL_WHOREPLY : Numeric.RPL_WHOSPCRPL;
        whox ??= "cuhsnfr";

        var response = new MultiResponse();
        foreach (var target in targets)
        {
            var args = new List<string>();

            if (whox.Contains('t'))
            {
                args.Add(token!);
            }

            if (whox.Contains('c'))
            {
                args.Add(channelResult);
            }

            if (whox.Contains('u'))
            {
                args.Add(target.Ident);
            }

            if (whox.Contains('i'))
            {
                args.Add((target.VirtualHost == null || client.HasPrivilege("oper:auspex:user")) ? target.RealIP.ToString() : "255.255.255.255");
            }

            if (whox.Contains('h'))
            {
                args.Add(target.DisplayHost);
            }

            if (whox.Contains('s'))
            {
                args.Add(client.Network.ServerName);
            }

            if (whox.Contains('n'))
            {
                args.Add(target.Nickname);
            }

            if (whox.Contains('f'))
            {
                var flags = new StringBuilder();

                if (client.HasCapability<PresenceCapability>())
                {
                    // TODO: support O (for offline) and M (for mobile) once we figure out how that should work
                    flags.Append(target.Away ? 'G' : 'H');
                }
                else
                {
                    flags.Append(target.Away ? 'G' : 'H');
                }
                
                if (target.HasPrivilege("oper:general"))
                {
                    flags.Append('*');
                }

                if (channelResult != "*")
                {
                    var privs = client.Network.Channels[channelResult.ToUpperInvariant()].GetPrivilegesFor(target);
                    if (privs.Contains("channel:*"))
                    {
                        flags.Append('&');
                    }
                    else if (privs.Contains("channel:op"))
                    {
                        flags.Append('@');
                    }
                    else if (privs.Contains("channel:halfop"))
                    {
                        flags.Append('%');
                    }
                    else if (privs.Contains("channel:voice"))
                    {
                        flags.Append('+');
                    }
                }

                args.Add(flags.ToString());
            }

            if (whox.Contains('d'))
            {
                args.Add("0");
            }

            if (whox.Contains('l'))
            {
                // TODO: show idle time here
                args.Add("0");
            }

            if (whox.Contains('a'))
            {
                args.Add(target.Account ?? "0");
            }

            if (whox.Contains('A'))
            {
                // TODO: show account ID
                args.Add("0");
            }

            if (whox.Contains('o'))
            {
                if (channelResult != "*")
                {
                    var privs = client.Network.Channels[channelResult.ToUpperInvariant()].GetPrivilegesFor(target);
                    if (privs.Contains("channel:*"))
                    {
                        args.Add("1");
                    }
                    else if (privs.Contains("channel:op"))
                    {
                        args.Add("10");
                    }
                    else if (privs.Contains("channel:halfop"))
                    {
                        args.Add("100");
                    }
                    else
                    {
                        args.Add("n/a");
                    }
                }
            }

            if (whox.Contains('r'))
            {
                // for standard WHO replies, this param should be the hop count followed by realname
                args.Add(numeric == Numeric.RPL_WHOREPLY ? $"0 {target.RealName}" : target.RealName);
            }

            response.AddNumeric(client, numeric, args.ToArray());
        }

        response.AddNumeric(client, Numeric.RPL_ENDOFWHO, command.Args[0]);
        return Task.FromResult<ICommandResponse>(response);
    }

    IReadOnlyDictionary<string, object?> IISupportTokenProvider.GetTokens(User client)
    {
        Dictionary<string, object?> dict = new();

        if (WhoxEnabled)
        {
            dict[ISupportToken.WHOX] = null;
        }

        return dict;
    }
}
