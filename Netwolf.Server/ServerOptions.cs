using Netwolf.Server.Capabilities;
using Netwolf.Server.Capabilities.Vendor;
using Netwolf.Server.ChannelModes;
using Netwolf.Server.Channels;
using Netwolf.Server.Commands;
using Netwolf.Server.Sasl;

namespace Netwolf.Server;

public sealed class ServerOptions
{
    public List<Type> EnabledCommands { get; set; }

    public List<Type> EnabledCapabilities { get; set; }

    public List<Type> EnabledChannelModes { get; set; }

    public List<Type> EnabledChannelTypes { get; set; }

    public List<Type> EnabledUserModes { get; set; }

    public List<string> EnabledFeatures { get; set; }

    public List<Type> EnabledSaslMechanisms { get; set; }

    public string? DefaultRealm { get; set; }

    public Dictionary<string, Type> RealmMap { get; set; }

    public ServerOptions()
    {
        EnabledCommands = [
            typeof(AuthenticateCommand),
            typeof(CapCommand),
            typeof(ListUsersCommand),
            typeof(MotdCommand),
            typeof(NickCommand),
            typeof(UserCommand),
            typeof(WhoCommand),
        ];

        EnabledCapabilities = [
            typeof(CapNotifyCapability),
            typeof(PresenceCapability),
            typeof(SaslCapability),
        ];

        EnabledChannelModes = [
            typeof(SecretChannelMode),
            typeof(TopicLockChannelMode),
        ];

        EnabledChannelTypes = [
            typeof(TextChannel),
        ];

        EnabledUserModes = [];

        EnabledFeatures = [
            "WHOX",
        ];

        EnabledSaslMechanisms = [
            typeof(Plain),
        ];

        RealmMap = [];
    }
}
