using Netwolf.Server.Attributes;
using Netwolf.Server.Channels;

namespace Netwolf.Server.ChannelModes;

[AppliesToChannel<TextChannel>]
public sealed class SecretChannelMode : ParameterlessChannelMode<SecretChannelMode>
{
    public override char ModeChar => 's';
}
