using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.BotFramework.State;

/// <summary>
/// Contains identifying information on a channel, such as its name, modes, and the bot's status.
/// These are not directly constructable and must be obtained via ChannelRecordLookup. 
/// The lookup service will always return the same ChannelRecord instance for a given channel,
/// so object identity (reference equality) can work.
/// </summary>
public class ChannelRecord
{
    public string Name { get; internal set; }
    public string Topic { get; internal set; }

    /// <summary>
    /// Channel modes, with mode letter as the key and mode value (if any) as the value.
    /// Modes without values will have null.
    /// </summary>
    public ImmutableDictionary<string, string?> Modes { get; internal set; }

    /// <summary>
    /// Users present in the channel along with their prefix (empty string if no privileges)
    /// </summary>
    public ImmutableDictionary<UserRecord, string> Users { get; internal set; }

    internal ChannelRecord(string name)
    {
        Name = name;
        Topic = string.Empty;
        Modes = ImmutableDictionary<string, string?>.Empty;
        Users = ImmutableDictionary<UserRecord, string>.Empty;
    }

    internal ChannelRecord(string name, string topic, IReadOnlyDictionary<string, string?> modes)
    {
        Name = name;
        Topic = topic;

        Modes = modes.ToImmutableDictionary();
        Users = ImmutableDictionary<UserRecord, string>.Empty;
    }
}
