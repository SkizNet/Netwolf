using Netwolf.Server.Attributes;
using Netwolf.Server.Channels;

namespace Netwolf.Server.ChannelModes;

[AppliesToChannel<TextChannel>]
public sealed class TopicLockChannelMode : ParameterlessChannelMode<TopicLockChannelMode>
{
    public override char ModeChar => 't';
}
