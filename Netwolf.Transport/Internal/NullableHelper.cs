using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Transport.Internal
{
    internal static class NullableHelper
    {
        internal static ValueTask DisposeAsyncIfNotNull(IAsyncDisposable? obj)
        {
            return obj?.DisposeAsync() ?? ValueTask.CompletedTask;
        }
    }
}
