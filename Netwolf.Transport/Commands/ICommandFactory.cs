// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later


// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

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
    ICommand CreateCommand(CommandType commandType, string? source, string verb, IReadOnlyList<string?> args, IReadOnlyDictionary<string, string?> tags, CommandCreationOptions? options = null);

    /// <summary>
    /// Parse a raw IRC protocol message
    /// </summary>
    /// <param name="commandType">Whether this is a client-generated or server-generated command</param>
    /// <param name="message">Message to parse, <i>without</i> the trailing CRLF</param>
    /// <returns></returns>
    ICommand Parse(CommandType commandType, string message);
}
