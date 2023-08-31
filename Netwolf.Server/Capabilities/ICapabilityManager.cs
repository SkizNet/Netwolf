using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Capabilities;

public interface ICapabilityManager
{
    IEnumerable<ICapability> GetAllCapabilities();

    bool ApplyCapabilitySet(User client, IEnumerable<string> add, IEnumerable<string> remove);
}
