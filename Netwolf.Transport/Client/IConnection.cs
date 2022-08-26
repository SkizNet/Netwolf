using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Transport.Client
{
    public interface IConnection : IDisposable, IAsyncDisposable
    {
    }
}
