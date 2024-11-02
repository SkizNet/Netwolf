// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.PluginFramework.Commands;
using Netwolf.Transport.Events;
using Netwolf.Transport.Exceptions;
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
    /// Event raised whenever we become disconnected for any reason. The
    /// <c>sender</c> parameter will be the exception(s) thrown, if any.
    /// </summary>
    event EventHandler<NetworkEventArgs>? Disconnected;

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
    /// Prepare a message to be sent to the target. If the message is long,
    /// it will be broken up into multiple commands.
    /// </summary>
    /// <param name="messageType">Type of message to send</param>
    /// <param name="target">Target; can be a nickname or a channel</param>
    /// <param name="text">Message text</param>
    /// <param name="tags">Message tags</param>
    /// <param name="sharedChannel">
    /// If CPRIVMSG/CNOTICE is supported by the ircd, pass in the name of a channel your user is
    /// opped or voiced in and that is shared with the target to use CPRIVMSG/CNOTICE instead of
    /// the PRIVMSG/NOTICE commands. If not supported by the ircd, this parameter does nothing.
    /// Many ircds will also automatically "promote" messages to CPRIVMSG/CNOTICE and this will
    /// be unnecessary for those ircds as well.
    /// </param>
    /// <returns>One or more commands to send the message to the target</returns>
    ICommand[] PrepareMessage(MessageType messageType, string target, string text, IReadOnlyDictionary<string, string?>? tags = null, string? sharedChannel = null);

    /// <summary>
    /// Prepare a command to be sent to the network.
    /// </summary>
    /// <param name="verb">Command to send.</param>
    /// <param name="args">
    /// Command arguments, which will be turned into strings.
    /// <c>null</c> values (whether before or after string conversion) will be omitted.
    /// </param>
    /// <param name="tags">
    /// Command tags. <c>null</c> values and empty strings will be sent without a tag value.
    /// </param>
    /// <returns>The prepared command, which can be sent to the network via <see cref="SendAsync(ICommand)"/>.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="verb"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">If <paramref name="verb"/> is invalid.</exception>
    /// <exception cref="ArgumentException">If a member of <paramref name="args"/> except for the final member would be considered a trailing argument.</exception>
    /// <exception cref="CommandTooLongException">
    /// If the expanded command (without tags) cannot fit within 512 bytes or the tags cannot fit within 4096 bytes.
    /// </exception>
    ICommand PrepareCommand(string verb, IEnumerable<object?>? args = null, IReadOnlyDictionary<string, string?>? tags = null);

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

    /// <summary>
    /// Process a command as if it was received from the network connection.
    /// Be very careful when using this method as it can corrupt internal state if used incorrectly.
    /// </summary>
    /// <param name="command"></param>
    /// <param name="cancellationToken"></param>
    void UnsafeReceiveCommand(ICommand command, CancellationToken cancellationToken);

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
