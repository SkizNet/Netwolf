// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.Commands;
using Netwolf.Transport.Events;
using Netwolf.Transport.State;

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
    IObservable<CommandEventArgs> CommandReceived { get; }

    /// <summary>
    /// Retrieves an immutable point-in-time snapshot of the network info of this network
    /// </summary>
    /// <returns></returns>
    INetworkInfo AsNetworkInfo();

    /// <summary>
    /// Delegate type for <see cref="ShouldEnableCap"/>.
    /// </summary>
    /// <param name="args">Arguments for the filter</param>
    /// <returns></returns>
    delegate bool CapFilter(CapEventArgs args);

    /// <summary>
    /// Callback to determine if a non-default CAP should be enabled.
    /// Default CAPs are unconditionally enabled and will not call this method.
    /// This delegate can be chained, and the CAP will be enabled should any of its registered
    /// callbacks returns true.
    /// </summary>
    CapFilter? ShouldEnableCap { get; set; }

    /// <summary>
    /// Event raised for each CAP that is enabled by the network. This is fired
    /// once per capability mentioned in the CAP ACK or CAP LIST commands. It is possible
    /// for this to fire multiple times for the same capability.
    /// </summary>
    IObservable<CapEventArgs> CapEnabled { get; }

    /// <summary>
    /// Event raised for each CAP that is no longer supported by the network.
    /// This is fired once per capability mentioned in the CAP DEL command.
    /// It is possible for this to fire on a cap that was not previously listed in a
    /// CapEnabled event, so listeners should not assume that the cap was previously enabled.
    /// </summary>
    IObservable<CapEventArgs> CapDisabled { get; }

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

    /// <summary>
    /// Sends a command to the network. Rate limiters will apply to the sent command.
    /// </summary>
    /// <param name="command"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    DeferredCommand SendAsync(ICommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a raw line to the network. The line will be parsed and validated and does not need to have a source defined.
    /// The verb, arguments, and tags from the raw line will be sent.
    /// Rate limiters will apply to the sent command.
    /// A CRLF will be automatically appended to the line and must not be present in the passed-in line.
    /// In general, prefer to use <see cref="SendAsync(ICommand, CancellationToken)"/> as it is more performant due to not requiring command parsing.
    /// </summary>
    /// <param name="rawLine">A line containing the verb, arguments, and message tags to send.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns></returns>
    DeferredCommand SendRawAsync(string rawLine, CancellationToken cancellationToken);

    /// <summary>
    /// Send a raw line to the network, with no validation and bypassing all rate limiters.
    /// This is NOT SAFE to use on user input and misuse may lead to security vulnerabilities.
    /// A CRLF is automatically appended to the end of the line, however lines with embedded CRLF
    /// are allowed and will be interpreted by the remote ircd as multiple commands.
    /// </summary>
    /// <param name="rawLine">A line that conforms to the IRC protocol. No validation or processing is performed.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns></returns>
    Task UnsafeSendRawAsync(string rawLine, CancellationToken cancellationToken);

    /// <summary>
    /// Disconnect from the network. This task cannot be cancelled.
    /// </summary>
    /// <param name="reason">Reason used in the QUIT message, displayed to others on the network</param>
    /// <returns></returns>
    Task DisconnectAsync(string? reason = null);

    /// <summary>
    /// Process a command as if it was received from the network connection.
    /// Be very careful when using this method as it can corrupt internal state if used incorrectly.
    /// </summary>
    /// <param name="command"></param>
    /// <param name="cancellationToken">Cancellation token to pass to async handlers.</param>
    /// <remarks>
    /// This method <em>does not block</em> and will execute async handlers in a "fire and forget" fashion.
    /// </remarks>
    void UnsafeReceiveRaw(string command, CancellationToken cancellationToken);

    /// <summary>
    /// Updates a user in the network state with the provided details, adding or removing them as necessary.
    /// Be very careful when using this method as it can corrupt internal state if used incorrectly.
    /// </summary>
    /// <param name="user"></param>
    void UnsafeUpdateUser(UserRecord user);

    /// <summary>
    /// Updates a channel in the network state with the provided details, adding or removing it as necessary.
    /// Be very careful when using this method as it can corrupt internal state if used incorrectly.
    /// </summary>
    /// <param name="channel"></param>
    void UnsafeUpdateChannel(ChannelRecord channel);
}
