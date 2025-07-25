using Netwolf.Transport.Context;
using Netwolf.Transport.IRC;
using Netwolf.Transport.State;

namespace Netwolf.Test.PluginFramework;

internal class TestContext : ExtensibleContextBase
{
    public override object Sender => null!;
    protected override INetworkInfo GetContextNetwork() => null!;
    protected override ChannelRecord? GetContextChannel() => null;
    protected override UserRecord? GetContextUser() => null;
}
