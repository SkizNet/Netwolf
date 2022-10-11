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
        /// Send a command to the remote server
        /// </summary>
        /// <param name="command">Command to send</param>
        /// <param name="cancellationToken">
        /// Cancellation token; passing <see cref="CancellationToken.None"/>
        /// will block indefinitely until the command is sent.
        /// </param>
        /// <returns></returns>
        Task SendAsync(ICommand command, CancellationToken cancellationToken);

        /// <summary>
        /// Close the underlying connection and free up related resources;
        /// this task cannot be cancelled.
        /// </summary>
        /// <returns></returns>
        Task DisconnectAsync();
    }
}
