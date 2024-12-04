using Netwolf.PluginFramework.Context;

namespace Netwolf.Test.PluginFramework;

internal class TestContext : IContext
{
    public object Sender { get; set; } = null!;
    public IValidationContextFactory? ValidationContextFactory => null;
}
