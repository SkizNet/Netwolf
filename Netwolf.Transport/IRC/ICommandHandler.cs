// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.Transport.IRC;

/// <summary>
/// Type-erased <see cref="ICommandHandler{TResult}"/> for use in nongeneric contexts
/// </summary>
public interface ICommandHandler
{
    string Command { get; }

    string? Privilege => null;

    string UnderlyingFullName => GetType().FullName ?? "<unknown>";
}
