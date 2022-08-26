using Netwolf.Transport.Internal;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Transport.Client
{
    public class IrcConnection : IConnection
    {
        private bool _disposed = false;

        private Stream? Stream { get; set; }

        private string HostName { get; init; }

        private int Port { get; init; }

        private bool Secure { get; init; }

        private bool AcceptAllCertificates { get; init; }

        private List<string> TrustedFingerprints { get; init; } = new List<string>();

        private bool CheckOnlineRevocation { get; init; }

        private X509Certificate2? ClientCertificate { get; set; }

        private EndPoint? BindHost { get; init; }

        internal IrcConnection(Network network, Server server, NetworkOptions options)
        {
            HostName = server.HostName;
            Port = server.Port;

            if (options.BindHost != null)
            {
                BindHost = IPEndPoint.Parse(options.BindHost);
            }

            Secure = server.SecureConnection;
            AcceptAllCertificates = options.AcceptAllCertificates;
            // normalize our trusted fingerprints to all-uppercase with no colon separators
            TrustedFingerprints.AddRange(from fp in options.TrustedFingerprints
                                         select fp.Replace(":", String.Empty).ToUpperInvariant());
            // additionally disable online revocation checks if we're trusting everything or if we have a
            // fingerprint list
            CheckOnlineRevocation = options.CheckOnlineRevocation
                || AcceptAllCertificates
                || TrustedFingerprints.Count > 0;
            ClientCertificate = network.AccountCertificate;
        }

        /// <summary>
        /// Connect with the default timeout (15 seconds).
        /// Throws TimeoutException if a connection cannot be made in time.
        /// </summary>
        /// <returns></returns>
        public async Task ConnectAsync()
        {
            using var source = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await ExceptionHelper.SuppressAsync<OperationCanceledException>(
                () => ConnectAsync(source.Token));

            if (source.IsCancellationRequested)
            {
                throw new TimeoutException("A timeout occurred while connecting to the remote host.");
            }
        }

        /// <summary>
        /// Connect to the remote host, with a user-controlled cancellation policy.
        /// </summary>
        /// <param name="cancellationToken">
        /// Cancellation token; passing <see cref="CancellationToken.None"/>
        /// will block indefinitely until the connection happens.
        /// </param>
        /// <returns></returns>
        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            // Attempt to set socket options; these may not be supported on all platforms so gracefully fail if
            // we cannot set these options.
            ExceptionHelper.Suppress<SocketException>(
                () => socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true));
            ExceptionHelper.Suppress<SocketException>(
                () => socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseUnicastPort, true));

            if (BindHost != null)
            {
                socket.Bind(BindHost);
            }

            await socket.ConnectAsync(HostName, Port, cancellationToken);
            if (!socket.Connected || cancellationToken.IsCancellationRequested)
            {
                // connect was canceled
                socket.Close();
            }

            cancellationToken.ThrowIfCancellationRequested();

            Stream = new NetworkStream(socket, ownsSocket: true);

            // establish TLS if desired
            if (Secure)
            {
                var sslStream = new SslStream(Stream, leaveInnerStreamOpen: false);
                // store in Stream so that future exceptions will result in the top-level stream being disposed
                // SslStream owns the NetworkStream which owns the Socket so there are no resource leaks
                Stream = sslStream;
                var sslOptions = new SslClientAuthenticationOptions()
                {
                    EncryptionPolicy = EncryptionPolicy.RequireEncryption,
                    TargetHost = HostName,
                    RemoteCertificateValidationCallback = VerifyServerCertificate,
                    CertificateRevocationCheckMode = CheckOnlineRevocation
                        ? X509RevocationMode.Online
                        : X509RevocationMode.NoCheck
                };

                if (ClientCertificate != null)
                {
                    sslOptions.ClientCertificates =
                        new X509CertificateCollection(new X509Certificate[] { ClientCertificate });
                }

                await sslStream.AuthenticateAsClientAsync(sslOptions, cancellationToken);
            }
        }

        private bool VerifyServerCertificate(
            object sender,
            X509Certificate? certificate,
            X509Chain? chain,
            SslPolicyErrors sslPolicyErrors)
        {
            if (certificate == null)
            {
                // a certificate needs to be presented, even if it's invalid
                return false;
            }

            if (AcceptAllCertificates)
            {
                // validation is disabled
                return true;
            }

            if (TrustedFingerprints.Count > 0)
            {
                // given an explicit list of fingerprints to trust
                // fingerprints not on this list are not trusted, even if they are otherwise valid
                var fingerprint = certificate.GetCertHashString(HashAlgorithmName.SHA256);
                return TrustedFingerprints.Contains(fingerprint);
            }

            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                // validation succeeded
                return true;
            }

            // something failed in validation
            return false;
        }

        /// <summary>
        /// Perform cleanup of managed resources asynchronously.
        /// </summary>
        /// <returns>Awaitable ValueTask for the async cleanup operation</returns>
        protected virtual async ValueTask DisposeAsyncCore()
        {
            await NullableHelper.DisposeAsyncIfNotNull(Stream).ConfigureAwait(false);
            ClientCertificate?.Dispose();

            Stream = null;
            ClientCertificate = null;
        }

        /// <summary>
        /// Perform cleanup of managed and unmanaged resources synchronously.
        /// </summary>
        /// <param name="disposing">If <c>true</c>, clean up managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    Stream?.Dispose();
                    ClientCertificate?.Dispose();

                    Stream = null;
                    ClientCertificate = null;
                }

                // Free unmanaged resources here
                
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            Dispose(disposing: false);
            GC.SuppressFinalize(this);
        }
    }
}
