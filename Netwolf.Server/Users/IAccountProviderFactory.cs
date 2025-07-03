using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Users;

public interface IAccountProviderFactory
{
    IAccountProvider GetAccountProvider(ReadOnlySpan<byte> username);
}
