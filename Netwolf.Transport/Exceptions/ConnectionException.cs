// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.Transport.Exceptions;

/// <summary>
/// Indicates a fatal error in an attempt to connect to a Network
/// </summary>
public class ConnectionException : Exception
{
    public ConnectionException() : base() { }
    public ConnectionException(string message) : base(message) { }
    public ConnectionException(string message, Exception innerException) : base(message, innerException) { }
}
