using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Internal;

[Flags]
internal enum RegistrationFlags
{
    None = 0,
    PendingPass = 1 << 0,
    PendingNick = 1 << 1,
    PendingUser = 1 << 2,
    PendingCapNegotation = 1 << 3,
    NeedsIdentLookup = 1 << 4,
    PendingIdentLookup = 1 << 5,
    NeedsHostLookup = 1 << 6,
    PendingHostLookup = 1 << 7,
    Default = PendingPass | PendingNick | PendingUser,
}
