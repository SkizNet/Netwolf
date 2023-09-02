using Microsoft.Extensions.DependencyInjection;

namespace Netwolf.Server.ISupport;

/// <summary>
/// Provides the default set of ISUPPORT tokens.
/// Keep this as an internal class.
/// </summary>
internal class DefaultTokenProvider : IISupportTokenProvider
{
    private IServiceProvider Provider { get; init; }

    public DefaultTokenProvider(IServiceProvider provider)
    {
        // FIXME: this is temporary until we get rid of the Network service
        // we should instead pull in the ChannelManager (or whatever it'd be called) to grab channel types,
        // and the options snapshot for network config to obtain the network name
        // relying on Network here causes a DI loop since Network needs an IISupportTokenProvider
        Provider = provider;
    }

    IReadOnlyDictionary<string, object?> IISupportTokenProvider.GetTokens(User client)
    {
        var network = Provider.GetRequiredService<Network>();

        return new Dictionary<string, object?>()
        {
            { "AWAYLEN", 350 },
            { "CASEMAPPING", "ascii" },
            { "CHANLIMIT", "#:100" },
            { "CHANMODES", "beIq,k,l,imnst" },
            { "CHANNELLEN", 30 },
            { "CHANTYPES", network.ChannelTypes },
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
            { "NETWORK", network.NetworkName },
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
