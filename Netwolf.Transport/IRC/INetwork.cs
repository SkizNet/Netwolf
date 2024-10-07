// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.PluginFramework.Commands;
using Netwolf.Transport.Exceptions;

namespace Netwolf.Transport.IRC;

/// <summary>
/// Represents a Network
/// </summary>
public interface INetwork : INetworkInfo, IDisposable, IAsyncDisposable
{
    /// <summary>
    /// True if we are currently connected to this Network
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Event raised whenever we receive a command from the network
    /// </summary>
    event EventHandler<NetworkEventArgs>? CommandReceived;

    /// <summary>
    /// Event raised whenever we become disconnected for any reason. The
    /// <c>sender</c> parameter will be the exception(s) thrown, if any.
    /// </summary>
    event EventHandler<NetworkEventArgs>? Disconnected;

    /// <summary>
    /// Event raised for each new CAP we receive from the network. This is fired
    /// once per capability mentioned in a CAP LS or CAP NEW command. Listeners
    /// can modify the <see cref="CapEventArgs.EnableCap"/> property to have
    /// us automatically send a CAP REQ once all event handlers have finished.
    /// </summary>
    event EventHandler<CapEventArgs>? CapReceived;

    /// <summary>
    /// Event raised for each CAP that is enabled by the network. This is fired
    /// once per capability mentioned in the CAP ACK or CAP LIST commands. It is possible
    /// for this to fire multiple times for the same capability.
    /// </summary>
    event EventHandler<CapEventArgs>? CapEnabled;

    /// <summary>
    /// Event raised for each CAP that is no longer supported by the network.
    /// This is fired once per capability mentioned in the CAP DEL command.
    /// It is possible for this to fire on a cap that was not previously listed in a
    /// CapEnabled event, so listeners should not assume that the cap was previously enabled.
    /// </summary>
    event EventHandler<CapEventArgs>? CapDisabled;

    /// <summary>
    /// Connect to the network and perform user registration. If the passed-in
    /// cancellation token has a timeout, that timeout will apply to all connection
    /// attempts, rather than any individual connection. Individual connection
    /// timeouts are controlled by the <see cref="NetworkOptions"/> passed in
    /// while creating the <see cref="INetwork"/>.
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancellation token; passing <see cref="CancellationToken.None"/>
    /// will retry connections indefinitely until the connection happens.
    /// </param>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    ICommand[] PrepareMessage(MessageType messageType, string target, string text, IReadOnlyDictionary<string, object?>? tags = null);

    /// <summary>
    /// Prepare a command to be sent to the network.
    /// </summary>
    /// <param name="verb">Command to send.</param>
    /// <param name="args">
    /// Command arguments, which will be turned into strings.
    /// <c>null</c> values (whether before or after string conversion) will be omitted.
    /// </param>
    /// <param name="tags">
    /// Command tags. <c>null</c> values will be sent without tag values, whereas all other values
    /// will be turned into strings. If the resultant value is an empty string, it will be sent without a tag value.
    /// </param>
    /// <returns>The prepared command, which can be sent to the network via <see cref="SendAsync(ICommand)"/>.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="verb"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">If <paramref name="verb"/> is invalid.</exception>
    /// <exception cref="ArgumentException">If a member of <paramref name="args"/> except for the final member would be considered a trailing argument.</exception>
    /// <exception cref="CommandTooLongException">
    /// If the expanded command (without tags) cannot fit within 512 bytes or the tags cannot fit within 4096 bytes.
    /// </exception>
    ICommand PrepareCommand(string verb, IEnumerable<object?>? args = null, IReadOnlyDictionary<string, object?>? tags = null);

    /// <summary>
    /// Send a command to the network
    /// </summary>
    /// <param name="command"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task SendAsync(ICommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send a raw command to the network.
    /// No validation or manipulation is performed; can be dangerous.
    /// DO NOT USE with untrusted input.
    /// </summary>
    /// <param name="command">Command to send which conforms to the IRC protocol</param>
    /// <param name="cancellationToken">
    /// Cancellation token; passing <see cref="CancellationToken.None"/>
    /// will block indefinitely until the command is sent.
    /// </param>
    /// <returns></returns>
    Task UnsafeSendRawAsync(string command, CancellationToken cancellationToken);

    /// <summary>
    /// Disconnect from the network. This task cannot be cancelled.
    /// </summary>
    /// <param name="reason">Reason used in the QUIT message, displayed to others on the network</param>
    /// <returns></returns>
    Task DisconnectAsync(string reason);
}
