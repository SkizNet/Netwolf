// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.Transport.Exceptions;

/// <summary>
/// Indicates an issue with the network's internal state.
/// The transport framework does not attempt to automatically recover from these errors,
/// however client code may wish to do so instead of simply crashing.
/// </summary>
public class BadStateException : Exception
{
    public BadStateException() { }

    public BadStateException(string? message)
        : base(message) { }

    public BadStateException(string? message, Exception? innerException)
        : base(message, innerException) { }
}
