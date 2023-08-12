using Netwolf.Server.Attributes;
using Netwolf.Server.Channels;

namespace Netwolf.Server.ChannelModes;

[AppliesToChannel<TextChannel>]
public sealed class TopicLockMode : ParameterlessChannelMode<TopicLockMode>
{
    public override char ModeChar => 't';
}
