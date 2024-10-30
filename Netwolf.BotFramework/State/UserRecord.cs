using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.BotFramework.State;

/// <summary>
/// Contains information on a user.
/// These are not directly constructable and must be obtained via UserRecordLookup. 
/// The lookup service will always return the same UserRecord instance for a given user,
/// so object identity (reference equality) can work.
/// </summary>
public class UserRecord
{
    public string Nick { get; internal set; }
    public string Ident { get; internal set; }
    public string Host { get; internal set; }
    public string? Account { get; internal set; }
    public bool IsAway { get; internal set; }
    public string RealName { get; internal set; }

    /// <summary>
    /// All channels the user belongs to, along with the user's prefix in those channels.
    /// The prefix will be an empty string if the user lacks channel privileges.
    /// Only channels shared with the bot will be present here.
    /// </summary>
    public ImmutableDictionary<ChannelRecord, string> Channels { get; internal set; }

    internal UserRecord(string nick, string ident, string host, string? account, string realName, bool isAway)
    {
        Nick = nick;
        Ident = ident;
        Host = host;
        Account = account;
        RealName = realName;
        IsAway = isAway;
        Channels = ImmutableDictionary<ChannelRecord, string>.Empty;
    }
}
