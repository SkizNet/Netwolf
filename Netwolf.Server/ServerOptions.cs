using Netwolf.Server.Capabilities;
using Netwolf.Server.Capabilities.Vendor;
using Netwolf.Server.ChannelModes;
using Netwolf.Server.Channels;
using Netwolf.Server.Commands;

namespace Netwolf.Server;

public sealed class ServerOptions
{
    public List<Type> EnabledCommands { get; set; }

    public List<Type> EnabledCapabilities { get; set; }

    public List<Type> EnabledChannelModes { get; set; }

    public List<Type> EnabledChannelTypes { get; set; }

    public List<Type> EnabledUserModes { get; set; }

    public List<string> EnabledFeatures { get; set; }

    public ServerOptions()
    {
        EnabledCommands = new()
        {
            typeof(CapCommand),
            typeof(ListUsersCommand),
            typeof(MotdCommand),
            typeof(NickCommand),
            typeof(UserCommand),
            typeof(WhoCommand)
        };

        EnabledCapabilities = new()
        {
            typeof(CapNotifyCapability),
            typeof(PresenceCapability)
        };

        EnabledChannelModes = new()
        {
            typeof(SecretChannelMode),
            typeof(TopicLockChannelMode)
        };

        EnabledChannelTypes = new()
        {
            typeof(TextChannel)
        };

        EnabledUserModes = new()
        {

        };

        EnabledFeatures = new()
        {
            "WHOX"
        };
    }
}
