// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.Exceptions;
using Netwolf.Transport.IRC;
using Netwolf.Transport.State;

namespace Netwolf.Transport.Commands;

/// <summary>
/// DI factory for <see cref="ICommand"/>
/// </summary>
public interface ICommandFactory
{
    Type ObjectType { get; }

    /// <summary>
    /// Prepare a command in the IRC protocol line format
    /// </summary>
    /// <param name="commandType">Type of command</param>
    /// <param name="source">Command source</param>
    /// <param name="verb">Command verb, normalized to all-uppercase</param>
    /// <param name="args">Command arguments, may be empty</param>
    /// <param name="tags">Command tags, may be empty</param>
    /// <param name="options">Options defining various protocol-level limits in commands</param>
    /// <returns>The processed command</returns>
    ICommand CreateCommand(
        CommandType commandType,
        string? source,
        string verb,
        IReadOnlyList<string?> args,
        IReadOnlyDictionary<string, string?> tags,
        CommandCreationOptions? options = null);

    /// <summary>
    /// Parse a raw IRC protocol message
    /// </summary>
    /// <param name="commandType">Whether this is a client-generated or server-generated command</param>
    /// <param name="message">Message to parse, <i>without</i> the trailing CRLF</param>
    /// <returns></returns>
    ICommand Parse(CommandType commandType, string message);

    /// <summary>
    /// Prepare a message to be sent to the target. If the message is long,
    /// it will be broken up into multiple commands.
    /// </summary>
    /// <param name="sender">User sending the message</param>
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
    /// <param name="options">Options defining various protocol-level limits in commands</param>
    /// <returns>One or more commands to send the message to the target</returns>
    ICommand[] PrepareClientMessage(
        UserRecord sender,
        MessageType messageType,
        string target,
        string text,
        IReadOnlyDictionary<string, string?>? tags = null,
        string? sharedChannel = null,
        CommandCreationOptions? options = null);

    /// <summary>
    /// Prepare a command to be sent to the network.
    /// </summary>
    /// <param name="sender">User sending the command</param>
    /// <param name="verb">Command to send</param>
    /// <param name="args">
    /// Command arguments, which will be turned into strings.
    /// <c>null</c> values (whether before or after string conversion) will be omitted.
    /// </param>
    /// <param name="tags">
    /// Command tags. <c>null</c> values and empty strings will be sent without a tag value.
    /// </param>
    /// <param name="options">Options defining various protocol-level limits in commands</param>
    /// <returns>The prepared command, which can be sent to the network via <see cref="SendAsync(ICommand)"/>.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="verb"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">If <paramref name="verb"/> is invalid.</exception>
    /// <exception cref="ArgumentException">If a member of <paramref name="args"/> except for the final member would be considered a trailing argument.</exception>
    /// <exception cref="CommandTooLongException">
    /// If the expanded command or tags cannot fit within the allowed limits.
    /// </exception>
    ICommand PrepareClientCommand(
        UserRecord sender,
        string verb,
        IEnumerable<object?>? args = null,
        IReadOnlyDictionary<string, string?>? tags = null,
        CommandCreationOptions? options = null);
}
