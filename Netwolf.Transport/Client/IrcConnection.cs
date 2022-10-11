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

        /// <summary>
        /// State for this connection
        /// </summary>
        public IrcConnectionState State { get; init; } = new();

        private Socket? Socket { get; set; }

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
        /// Connect to the remote host
        /// </summary>
        /// <param name="cancellationToken">
        /// Cancellation token; passing <see cref="CancellationToken.None"/>
        /// will block indefinitely until the connection happens.
        /// </param>
        /// <returns></returns>
        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            if (Socket != null || Stream != null)
            {
                throw new InvalidOperationException("Connection has already been established.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            Socket = new Socket(SocketType.Stream, ProtocolType.Tcp);

            // Attempt to set socket options; these may not be supported on all platforms so gracefully fail if
            // we cannot set these options.
            ExceptionHelper.Suppress<SocketException>(
                () => Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseUnicastPort, true));

            if (BindHost != null)
            {
                Socket.Bind(BindHost);
            }

            await Socket.ConnectAsync(HostName, Port, cancellationToken);
            if (!Socket.Connected || cancellationToken.IsCancellationRequested)
            {
                // connect was canceled
                Socket.Close();
                Socket = null;
                throw new OperationCanceledException();
            }

            Stream = new NetworkStream(Socket, ownsSocket: false);

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
        /// Close the underlying connection and free up related resources;
        /// this task cannot be cancelled.
        /// </summary>
        /// <returns></returns>
        public async Task DisconnectAsync()
        {
            Socket?.Shutdown(SocketShutdown.Both);
            Socket?.Close();
            await NullableHelper.DisposeAsyncIfNotNull(Stream).ConfigureAwait(false);

            Socket = null;
            Stream = null;
        }

        /// <summary>
        /// Send a command to the remote server
        /// </summary>
        /// <param name="command">Command to send</param>
        /// <param name="cancellationToken">
        /// Cancellation token; passing <see cref="CancellationToken.None"/>
        /// will block indefinitely until the command is sent.
        /// </param>
        /// <returns></returns>
        public async Task SendAsync(ICommand command, CancellationToken cancellationToken)
        {
            if (Stream == null)
            {
                throw new InvalidOperationException("Cannot send to a closed connection.");
            }

            // TODO: do we need to queue this somehow? How do we know the stream is writable at this exact moment?
            await Stream.WriteAsync(command.FullCommand.EncodeUtf8(), cancellationToken);
        }

        /// <summary>
        /// Perform cleanup of managed resources asynchronously.
        /// </summary>
        /// <returns>Awaitable ValueTask for the async cleanup operation</returns>
        protected virtual async ValueTask DisposeAsyncCore()
        {
            await DisconnectAsync();
            ClientCertificate?.Dispose();

            Socket = null;
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
                    Socket?.Shutdown(SocketShutdown.Both);
                    Socket?.Close(); // calls Socket.Dispose() internally
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
