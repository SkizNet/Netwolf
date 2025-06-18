using Netwolf.Transport.Context;

namespace Netwolf.Test.PluginFramework;

internal class TestContext : ExtensibleContextBase
{
    public override object Sender => null!;
}
