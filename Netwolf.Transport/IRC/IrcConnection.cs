// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.Extensions.Logging;

using Netwolf.PluginFramework.Commands;
using Netwolf.Transport.Extensions;
using Netwolf.Transport.Internal;

using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication.ExtendedProtection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Netwolf.Transport.IRC;

public class IrcConnection : IConnection
{
    private bool _disposed = false;

    private Socket? Socket { get; set; }

    private Stream? Stream { get; set; }

    private PipeReader? Reader { get; set; }

    private PipeWriter? Writer { get; set; }

    private string HostName { get; init; }

    private int Port { get; init; }

    private bool Secure { get; init; }

    private bool AcceptAllCertificates { get; init; }

    private List<string> TrustedFingerprints { get; init; } = new List<string>();

    private bool CheckOnlineRevocation { get; init; }

    private X509Certificate2? ClientCertificate { get; set; }

    private EndPoint? BindHost { get; init; }

    private ICommandFactory CommandFactory { get; init; }

    private ILogger<IConnection> Logger { get; init; }

    internal IrcConnection(
        IServer server,
        NetworkOptions options,
        ILogger<IConnection> logger,
        ICommandFactory commandFactory)
    {
        HostName = server.HostName;
        Port = server.Port;
        CommandFactory = commandFactory;
        Logger = logger;

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

        if (!String.IsNullOrEmpty(options.AccountCertificateFile))
        {
            try
            {
                ClientCertificate = new X509Certificate2(options.AccountCertificateFile, options.AccountCertificatePassword);
            }
            catch (CryptographicException ex)
            {
                logger.LogWarning("Cannot load TLS client certificate {AccountCertificateFile}: {Message}", options, ex);
            }
        }
    }

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

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

        // Allocate a buffer large enough to hold one maximum-sized incoming IRC message with client and server tags,
        // rounded up to the nearest multiple of 4 KiB (typical memory page size). If the buffer has fewer than
        // 512 bytes remaining in it, a larger buffer will be allocated during the next read, up to 2 MiB total.
        Reader = PipeReader.Create(Stream, new StreamPipeReaderOptions(bufferSize: 12288, minimumReadSize: 512, leaveOpen: true));

        // All writes to Writer get immediately written to the underlying Stream, so buffer sizes don't matter here.
        Writer = PipeWriter.Create(Stream, new StreamPipeWriterOptions(leaveOpen: true));
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
            string fingerprint = certificate.GetCertHashString(HashAlgorithmName.SHA256);
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

    /// <inheritdoc />
    public async Task DisconnectAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await Task.Run(Disconnect).ConfigureAwait(false);
    }

    private void Disconnect()
    {
        Reader?.CancelPendingRead();
        Reader?.Complete();
        Writer?.CancelPendingFlush();
        Writer?.Complete();
        Socket?.Shutdown(SocketShutdown.Both);
        Socket?.Close(); // calls Socket.Dispose() internally
        Stream?.Dispose();
        Socket = null;
        Stream = null;
        Reader = null;
        Writer = null;
    }

    /// <inheritdoc />
    public Task SendAsync(ICommand command, CancellationToken cancellationToken)
    {
        return UnsafeSendRawAsync(command.FullCommand, cancellationToken);
    }

    private static readonly byte[] _crlf = "\r\n".EncodeUtf8();

    public async Task UnsafeSendRawAsync(string command, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (Writer == null)
        {
            throw new InvalidOperationException("Cannot send to a closed connection.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        Logger.LogDebug("--> {Command}", command);
        await Writer.WriteAsync(command.EncodeUtf8(), cancellationToken);
        await Writer.WriteAsync(_crlf, cancellationToken);
    }

    private string? ExtractMessage(ref ReadOnlySequence<byte> buffer)
    {
        var sequenceReader = new SequenceReader<byte>(buffer);
        if (sequenceReader.TryReadTo(out ReadOnlySpan<byte> span, _crlf, advancePastDelimiter: true))
        {
            // we need to decode command before advancing Reader to avoid memory corruption
            var sb = new StringBuilder(span.DecodeUtf8(strict: false));
            Reader!.AdvanceTo(sequenceReader.Position);
            // ICommandFactory.Parse requires that the message ends with CRLF
            sb.Append("\r\n");
            return sb.ToString();
        }
        else if (buffer.Length > 8192 + 512)
        {
            // didn't find a CRLF combo and we've read more than the maximum size of an IRC command
            throw new ProtocolViolationException("Remote IRC command too long.");
        }
        else
        {
            // didn't find a CRLF combo, so mark the bytes as examined and let the reader keep reading
            Reader!.AdvanceTo(buffer.Start, buffer.End);
            return null;
        }
    }

    public async Task<ICommand> ReceiveAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (Reader == null)
        {
            throw new InvalidOperationException("Cannot receive from a closed connection.");
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await Reader.ReadAsync(cancellationToken);
            if (result.IsCanceled)
            {
                throw new OperationCanceledException("ReceiveAsync cancelled by call to DisconnectAsync or Dispose.");
            }

            // Find the CRLF signalling end of message
            var buffer = result.Buffer;
            // Side-effect: advances Reader, making buffer no longer safe to use after this call finishes
            string? message = ExtractMessage(ref buffer);
            if (message != null)
            {
                Logger.LogDebug("<-- {Command}", message);
                return CommandFactory.Parse(CommandType.Server, message);
            }
        }
    }

    public ChannelBinding? GetChannelBinding(ChannelBindingKind kind)
    {
        if (Stream is SslStream sslStream)
        {
            try
            {
                return sslStream.TransportContext.GetChannelBinding(kind);
            }
            catch (NotSupportedException)
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// Perform cleanup of managed resources asynchronously.
    /// </summary>
    /// <returns>Awaitable ValueTask for the async cleanup operation</returns>
    protected virtual async ValueTask DisposeAsyncCore()
    {
        await DisconnectAsync();
        ClientCertificate?.Dispose();
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
                Disconnect();
                ClientCertificate?.Dispose();
                ClientCertificate = null;
            }

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
