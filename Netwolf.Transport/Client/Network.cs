
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Netwolf.Transport.Internal;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Transport.Client
{
    /// <summary>
    /// A network that we connect to as a client
    /// </summary>
    public class Network : INetwork
    {
        private bool _disposed;

        /// <summary>
        /// Network options defined by the user
        /// </summary>
        protected NetworkOptions Options { get; set; }

        /// <summary>
        /// Logger for this Network
        /// </summary>
        protected ILogger<Network> Logger { get; init; }

        private Connection? _connection;

        /// <summary>
        /// A connection to the network
        /// </summary>
        protected Connection Connection => _connection ?? throw new InvalidOperationException("");

        /// <summary>
        /// User-defined network name (not necessarily what the network actually calls itself)
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        /// TLS client certificate used to log into the user's account.
        /// </summary>
        protected internal X509Certificate2? AccountCertificate { get; private set; }

        /// <summary>
        /// Create a new Network that can be connected to.
        /// </summary>
        /// <param name="name">
        /// Name of the network, for the caller's internal tracking purposes.
        /// The name does not need to be unique.
        /// </param>
        /// <param name="options">Network options.</param>
        /// <param name="logger">Logger to use.</param>
        public Network(string name, NetworkOptions options, ILogger<Network> logger)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(options);

            Name = name;
            Options = options;
            Logger = logger;

            if (!String.IsNullOrEmpty(Options.AccountCertificateFile))
            {
                try
                {
                    AccountCertificate = new X509Certificate2(Options.AccountCertificateFile, Options.AccountCertificatePassword);
                }
                catch (CryptographicException ex)
                {
                    Logger.LogWarning("Cannot load TLS client certificate {AccountCertificateFile}: {Message}", Options, ex);
                }
                finally
                {
                    Options.AccountCertificatePassword = null;
                }
            }
        }

        /// <summary>
        /// Perform cleanup of managed resources asynchronously.
        /// </summary>
        /// <returns>Awaitable ValueTask for the async cleanup operation</returns>
        protected virtual async ValueTask DisposeAsyncCore()
        {
            await NullableHelper.DisposeAsyncIfNotNull(_connection).ConfigureAwait(false);
            AccountCertificate?.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            Dispose(disposing: false);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    AccountCertificate?.Dispose();
                }

                AccountCertificate = null;
                _disposed = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
