using Netwolf.Server.Attributes;
using Netwolf.Server.Channels;

namespace Netwolf.Server.ChannelModes;

[AppliesToChannel<TextChannel>]
public sealed class SecretMode : ParameterlessChannelMode<SecretMode>
{
    public override char ModeChar => 's';
}
