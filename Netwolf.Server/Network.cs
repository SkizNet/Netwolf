using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server;

public class Network
{
    public string NetworkName => "Netwolf Test";

    public string ServerName => "irc.netwolf.org";

    public string Version => "netwolf-0.1.0";

    public string UserModes => "iowx";

    public string ChannelModes => "beIiklmnostv";

    public string ChannelModesWithParams => "beIklov";

    public Dictionary<string, User> Clients { get; init; } = new();

    public Dictionary<string, Channel> Channels { get; init; } = new();
}
