using Netwolf.Transport.Internal;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Transport.Client
{
    public interface IConnection : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// Connect to the remote host
        /// </summary>
        /// <param name="cancellationToken">
        /// Cancellation token; passing <see cref="CancellationToken.None"/>
        /// will block indefinitely until the connection happens.
        /// </param>
        /// <returns></returns>
        Task ConnectAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Close the underlying connection and free up related resources;
        /// this task cannot be cancelled.
        /// </summary>
        /// <returns></returns>
        Task DisconnectAsync();
    }
}
