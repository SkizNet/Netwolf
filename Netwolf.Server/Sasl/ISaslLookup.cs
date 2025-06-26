using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Sasl;

public interface ISaslLookup
{
    IEnumerable<ISaslMechanismProvider> AllMechanisms { get; }

    bool TryGet(string name, [NotNullWhen(true)] out ISaslMechanismProvider? mechanism);
}
