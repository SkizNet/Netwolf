using Microsoft.Extensions.Options;

using Netwolf.Server.Sasl;

namespace Netwolf.Server.Capabilities;

public class SaslCapability : ICapability
{
    private ISaslLookup Lookup { get; init; }

    public string Name => "sasl";

    string? ICapability.Value => string.Join(',', Lookup.AllMechanisms.Select(m => m.Name).Order());

    public SaslCapability(ISaslLookup lookup)
    {
        Lookup = lookup;
    }
}
