using Netwolf.Server.Commands;
using Netwolf.Transport.Client;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server;

public class Network
{
    internal ICommandFactory CommandFactory { get; init; }

    internal ICommandDispatcher CommandDispatcher { get; init; }

    public string NetworkName => "NetwolfTest";

    public string ServerName => "irc.netwolf.org";

    public string Version => "netwolf-0.1.0";

    public string UserModes => "iowx";

    public string ChannelModes => "behIiklmnoqstv";

    public string ChannelModesWithParams => "behIkloqv";

    public Dictionary<string, User> Clients { get; init; } = new();

    public Dictionary<string, Channel> Channels { get; init; } = new();

    public Network(ICommandFactory commandFactory, ICommandDispatcher dispatcher)
    {
        CommandFactory = commandFactory;
        CommandDispatcher = dispatcher;
    }

    internal MultiResponse ReportISupport(User client)
    {
        var batch = new MultiResponse();

        // TODO: Make a lot of these configurable
        var tokens = new OrderedDictionary()
        {
            { "AWAYLEN", 350 },
            { "CASEMAPPING", "ascii" },
            { "CHANLIMIT", "#:100" },
            { "CHANMODES", "beIq,k,l,imnst" },
            { "CHANNELLEN", 30 },
            { "CHANTYPES", "#" },
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
            { "NETWORK", NetworkName },
            { "NICKLEN", 20 },
            { "PREFIX", "(ohv)@%+" },
            { "SAFELIST", null },
            { "SILENCE", 100 },
            { "STATUSMSG", "@%+" },
            { "TARGMAX", "ACCEPT:,JOIN:,KICK:1,LIST:1,MONITOR:,NAMES:1,NOTICE:4,PART:,PRIVMSG:4,WHOIS:1" },
            { "TOPICLEN", 350 },
            { "UTF8ONLY", null },
            { "USERLEN", 11 },
            { "WHOX", null }
        };

        for (int i = 0; i < tokens.Count; i += 10)
        {
            var slice = tokens
                .Cast<DictionaryEntry>()
                .Skip(i)
                .Take(10)
                .Select(e => e.Value != null ? $"{e.Key}={e.Value}" : e.Key.ToString()!)
                .ToArray();

            batch.AddNumeric(client, Numeric.RPL_ISUPPORT, slice);
        }

        return batch;
    }
}
