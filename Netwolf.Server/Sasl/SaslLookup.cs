using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Netwolf.Server.Internal;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Sasl;

internal class SaslLookup : ISaslLookup
{
    IEnumerable<ISaslMechanismProvider> ISaslLookup.AllMechanisms => Mechanisms;

    private List<ISaslMechanismProvider> Mechanisms { get; init; }

    public SaslLookup(IServiceProvider provider, ILogger<ISaslLookup> logger, IOptionsSnapshot<ServerOptions> options)
    {
        Mechanisms = TypeDiscovery.GetTypes<ISaslMechanismProvider>(provider, logger, options).ToList();
    }

    public bool TryGet(string name, [NotNullWhen(true)] out ISaslMechanismProvider? mechanism)
    {
        mechanism = Mechanisms.FirstOrDefault(m => m.Name == name);
        return mechanism is not null;
    }
}
