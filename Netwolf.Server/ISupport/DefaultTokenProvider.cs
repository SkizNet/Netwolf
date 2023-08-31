using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.ISupport;

/// <summary>
/// Provides the default set of ISUPPORT tokens.
/// Keep this as an internal class.
/// </summary>
internal class DefaultTokenProvider : IISupportTokenProvider
{
    private Network Network { get; init; }

    public DefaultTokenProvider(Network network)
    {
        Network = network;
    }

    IReadOnlyDictionary<string, object?> IISupportTokenProvider.GetTokens(User client)
    {
        return new Dictionary<string, object?>()
        {
            { "AWAYLEN", 350 },
            { "CASEMAPPING", "ascii" },
            { "CHANLIMIT", "#:100" },
            { "CHANMODES", "beIq,k,l,imnst" },
            { "CHANNELLEN", 30 },
            { "CHANTYPES", Network.ChannelTypes },
            { "ELIST", "CMNTU" },
            { "EXCEPTS", "e" },
            { "EXTBAN", "$,a" },
            { "HOSTLEN", 64 },
            { "INVEX", "I" },
            { "KICKLEN", 350 },
            { "MAXLIST", "beq:100,I:100" },
            { "MODES", 4 },
            { "MONITOR", 100 },
            { "NAMELEN", 150 },
            { "NETWORK", Network.NetworkName },
            { "NICKLEN", 20 },
            { "PREFIX", "(aohv)&@%+" },
            { "SAFELIST", null },
            { "SILENCE", 100 },
            { "STATUSMSG", "&@%+" },
            { "TARGMAX", "ACCEPT:,JOIN:,KICK:1,LIST:1,MONITOR:,NAMES:1,NOTICE:4,PART:,PRIVMSG:4,WHOIS:1" },
            { "TOPICLEN", 350 },
            { "UTF8ONLY", null },
            { "USERLEN", 11 }
        };
    }

    object? IISupportTokenProvider.MergeTokens(string key, IEnumerable<object> tokens)
    {
        return key switch
        {
            "TARGMAX" => String.Join(",", tokens),
            _ => null,
        };
    }
}
