using Netwolf.Server.Attributes;
using Netwolf.Server.ChannelModes;

namespace Netwolf.Server;

/// <summary>
/// Base class for all channel types recognized by the server
/// </summary>
public abstract class Channel
{
    /// <summary>
    /// Network this Channel belongs to
    /// </summary>
    public Network Network { get; init; }

    /// <summary>
    /// Channel name, including type prefix
    /// </summary>
    public string Name { get; private init; }

    /// <summary>
    /// Currently-active channel modes
    /// </summary>
    protected HashSet<IChannelMode> Modes { get; private init; } = new();

    // Backing field for Members
    private readonly Dictionary<User, HashSet<string>> _members = new();

    /// <summary>
    /// Channel members and their channel privileges.
    /// </summary>
    public IReadOnlyDictionary<User, HashSet<string>> Members => _members.AsReadOnly();

    /// <summary>
    /// Channel admins.
    /// </summary>
    public IEnumerable<User> Administrators => Members.Where(o => o.Value.Contains("channel:*")).Select(o => o.Key);

    /// <summary>
    /// Users explicitly flagged as "opped" on the channel.
    /// Does not include admins who have every channel privilege as a wildcard.
    /// </summary>
    public IEnumerable<User> Operators => Members.Where(o => o.Value.Contains("channel:op")).Select(o => o.Key);

    /// <summary>
    /// Users explicitly flagged as "halfopped" on the channel.
    /// Does not include admins who have every channel privilege as a wildcard.
    /// </summary>
    public IEnumerable<User> HalfOperators => Members.Where(o => o.Value.Contains("channel:halfop")).Select(o => o.Key);

    /// <summary>
    /// Users explicitly flagged as "voiced" on the channel.
    /// Does not include admins who have every channel privilege as a wildcard.
    /// </summary>
    public IEnumerable<User> VoicedUsers => Members.Where(o => o.Value.Contains("channel:voice")).Select(o => o.Key);

    public Channel(Network network, string name)
    {
        Network = network;
        Name = name;
    }

    /// <summary>
    /// Determine whether a mode can be applied to this type of channel.
    /// <para>
    /// This is typically done by decorating each mode type with the AppliesToChannel&lt;&gt;
    /// attribute, but may also be performed by overriding this method in a subclass. The latter
    /// may be required when introducing new channel types in other assemblies that wish to re-use
    /// built-in modes from this assembly.
    /// </para>
    /// <para>
    /// When overriding, you should call the base method to ensure that AppliesToChannel attributes
    /// continue to function properly.
    /// </para>
    /// </summary>
    /// <typeparam name="T">Mode type</typeparam>
    /// <returns></returns>
    public virtual bool AcceptsMode<T>()
        where T : IChannelMode
    {
        return typeof(T)
            .GetCustomAttributes(typeof(AppliesToChannelAttribute<>), inherit: true)
            .Cast<IAppliesTo>()
            .Any(attr => attr.CanApply(this));
    }

    public HashSet<string> GetPrivilegesFor(User user)
    {
        return Members.GetValueOrDefault(user, new HashSet<string>());
    }

    public bool HasMode<T>()
        where T : IChannelMode
    {
        return Modes.Any(m => m is T);
    }
}
