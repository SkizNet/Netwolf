using Netwolf.Server.Commands;
using Netwolf.Server.ISupport;
using Netwolf.Transport.Commands;

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server;

public class Network
{
    protected ICommandFactory CommandFactory { get; init; }

    protected IISupportResolver ISupportResolver { get; init; }

    public string NetworkName => "NetwolfTest";

    public string ServerName => "irc.netwolf.org";

    public string Version => "netwolf-0.1.0";

    public string UserModes => "iowx";

    public string ChannelModes => "behIiklmnoqstv";

    public string ChannelModesWithParams => "behIkloqv";

    public string ChannelTypes => "#";

    public ConcurrentDictionary<string, User> Clients { get; init; } = new();

    public ConcurrentDictionary<string, Channel> Channels { get; init; } = new();

    public int UserCount => Clients.Count;

    public int ChannelCount => Channels.Count;

    public int InvisibleCount => Clients.Count(c => c.Value.Invisible);

    internal int _maxUserCount = 0;
    public int MaxUserCount => _maxUserCount;

    internal int _pendingCount = 0;
    public int PendingCount => _pendingCount;

    public Network(ICommandFactory commandFactory, IISupportResolver iSupportResolver)
    {
        CommandFactory = commandFactory;
        ISupportResolver = iSupportResolver;
    }

    internal MultiResponse ReportISupport(User client)
    {
        var batch = new MultiResponse();
        var tokens = ISupportResolver.Resolve(client);

        for (int i = 0; i < tokens.Count; i += 10)
        {
            var slice = tokens
                .Skip(i)
                .Take(10)
                .Select(e => e.Value != null ? $"{e.Key}={e.Value}" : e.Key)
                .ToArray();

            batch.AddNumeric(client, Numeric.RPL_ISUPPORT, slice);
        }

        return batch;
    }
}
