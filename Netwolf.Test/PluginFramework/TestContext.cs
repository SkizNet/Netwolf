using Netwolf.Transport.Context;
using Netwolf.Transport.IRC;
using Netwolf.Transport.State;

namespace Netwolf.Test.PluginFramework;

internal class TestContext : ExtensibleContextBase
{
    public override object Sender => null!;
    public override INetworkInfo Network => null!;
    public override ChannelRecord? Channel => null;
    public override UserRecord? User => null;
}
