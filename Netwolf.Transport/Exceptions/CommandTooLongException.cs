// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.Transport.Exceptions;

/// <summary>
/// Indicates an error due to a command being over the allowed protocol limits
/// </summary>
public class CommandTooLongException : Exception
{
    public CommandTooLongException() : base() { }
    public CommandTooLongException(string message) : base(message) { }
    public CommandTooLongException(string message, Exception innerException) : base(message, innerException) { }
}
