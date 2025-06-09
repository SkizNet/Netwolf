using Netwolf.PluginFramework.Context;

namespace Netwolf.Test.PluginFramework;

internal class TestContext : ExtensibleContextBase
{
    public override object Sender => null!;
}
