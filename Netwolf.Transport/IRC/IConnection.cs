﻿namespace Netwolf.Transport.IRC;

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
    /// Send a raw command to the remote server.
    /// No validation or manipulation is performed; can be dangerous.
    /// DO NOT USE with untrusted input.
    /// </summary>
    /// <param name="command">Command to send which conforms to the IRC protocol</param>
    /// <param name="cancellationToken">
    /// Cancellation token; passing <see cref="CancellationToken.None"/>
    /// will block indefinitely until the command is sent.
    /// </param>
    /// <returns></returns>
    Task UnsafeSendAsync(string command, CancellationToken cancellationToken);

    /// <summary>
    /// Receive a command from the remote server
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancellation token; passing <see cref="CancellationToken.None"/>
    /// will block indefinitely until the command is received.
    /// </param>
    /// <returns>Command received from the remote server.</returns>
    Task<ICommand> ReceiveAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Close the underlying connection and free up related resources;
    /// this task cannot be cancelled.
    /// </summary>
    /// <returns></returns>
    Task DisconnectAsync();
}