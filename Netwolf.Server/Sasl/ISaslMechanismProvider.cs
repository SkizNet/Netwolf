using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Sasl;

public interface ISaslMechanismProvider
{
    string Name { get; }

    ISaslState InitializeForUser(User client);
}
