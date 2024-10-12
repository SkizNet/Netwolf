using Microsoft.Extensions.DependencyInjection;

using Netwolf.Transport.IRC;

namespace Netwolf.Server.ISupport;

/// <summary>
/// Provides the default set of ISUPPORT tokens.
/// Keep this as an internal class.
/// </summary>
internal class DefaultTokenProvider : IISupportTokenProvider
{
    private IServiceProvider Provider { get; init; }

    // Keep this in sync with GetTokens
    IEnumerable<ISupportToken> IISupportTokenProvider.ProvidedTokens => [
        ISupportToken.AWAYLEN,
        ISupportToken.CASEMAPPING,
        ISupportToken.CHANLIMIT,
        ISupportToken.CHANMODES,
        ISupportToken.CHANNELLEN,
        ISupportToken.CHANTYPES,
        ISupportToken.ELIST,
        ISupportToken.EXCEPTS,
        ISupportToken.EXTBAN,
        ISupportToken.HOSTLEN,
        ISupportToken.INVEX,
        ISupportToken.KICKLEN,
        ISupportToken.MAXLIST,
        ISupportToken.MODES,
        ISupportToken.MONITOR,
        ISupportToken.NAMELEN,
        ISupportToken.NETWORK,
        ISupportToken.NICKLEN,
        ISupportToken.PREFIX,
        ISupportToken.SAFELIST,
        ISupportToken.SILENCE,
        ISupportToken.STATUSMSG,
        ISupportToken.TARGMAX,
        ISupportToken.TOPICLEN,
        ISupportToken.UTF8ONLY,
        ISupportToken.USERLEN,
        ];

    public DefaultTokenProvider(IServiceProvider provider)
    {
        // FIXME: this is temporary until we get rid of the Network service
        // we should instead pull in the ChannelManager (or whatever it'd be called) to grab channel types,
        // and the options snapshot for network config to obtain the network name
        // relying on Network here causes a DI loop since Network needs an IISupportTokenProvider
        Provider = provider;
    }

    IReadOnlyDictionary<ISupportToken, object?> IISupportTokenProvider.GetTokens(User client)
    {
        var network = Provider.GetRequiredService<Network>();

        // Keep this in sync with ProvidedTokens
        return new Dictionary<ISupportToken, object?>()
        {
            { ISupportToken.AWAYLEN, 350 },
            { ISupportToken.CASEMAPPING, "ascii" },
            { ISupportToken.CHANLIMIT, "#:100" },
            { ISupportToken.CHANMODES, "beIq,k,l,imnst" },
            { ISupportToken.CHANNELLEN, 30 },
            { ISupportToken.CHANTYPES, network.ChannelTypes },
            { ISupportToken.ELIST, "CMNTU" },
            { ISupportToken.EXCEPTS, "e" },
            { ISupportToken.EXTBAN, "$,a" },
            { ISupportToken.HOSTLEN, 64 },
            { ISupportToken.INVEX, "I" },
            { ISupportToken.KICKLEN, 350 },
            { ISupportToken.MAXLIST, "beq:100,I:100" },
            { ISupportToken.MODES, 4 },
            { ISupportToken.MONITOR, 100 },
            { ISupportToken.NAMELEN, 150 },
            { ISupportToken.NETWORK, network.NetworkName },
            { ISupportToken.NICKLEN, 20 },
            { ISupportToken.PREFIX, "(aohv)&@%+" },
            { ISupportToken.SAFELIST, null },
            { ISupportToken.SILENCE, 100 },
            { ISupportToken.STATUSMSG, "&@%+" },
            { ISupportToken.TARGMAX, "ACCEPT:,JOIN:,KICK:1,LIST:1,MONITOR:,NAMES:1,NOTICE:4,PART:,PRIVMSG:4,WHOIS:1" },
            { ISupportToken.TOPICLEN, 350 },
            { ISupportToken.UTF8ONLY, null },
            { ISupportToken.USERLEN, 11 }
        };
    }

    object? IISupportTokenProvider.MergeTokens(ISupportToken key, IEnumerable<object> tokens)
    {
        return key switch
        {
            { Name: "TARGMAX" } => String.Join(",", tokens),
            _ => null,
        };
    }
}
