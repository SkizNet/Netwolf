using Microsoft.Extensions.Options;

namespace Netwolf.Server.Capabilities;

public class SaslCapability : ICapability
{
    private IOptionsSnapshot<ServerOptions> Options { get; init; }

    public string Name => "sasl";

    string? ICapability.Value => string.Join(',', Options.Value.EnabledSaslMechanisms);

    public SaslCapability(IOptionsSnapshot<ServerOptions> options)
    {
        Options = options;
    }
}
