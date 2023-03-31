using Netwolf.Server.Attributes;
using Netwolf.Server.Channels;

namespace Netwolf.Server.ChannelModes;

[AppliesToChannel<TextChannel>]
public sealed class TopicLock : ParameterlessChannelMode<TopicLock>
{
    public override char ModeChar => 't';
}
