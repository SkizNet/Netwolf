// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.Commands;
using Netwolf.Transport.IRC;

namespace Netwolf.Transport.Events;

public sealed class CommandEventArgs
{
    /// <summary>
    /// Network the event was raised for. Read-only.
    /// </summary>
    public INetwork Network { get; init; }

    /// <summary>
    /// Command being processed. Read-only
    /// </summary>
    public ICommand Command { get; init; }

    /// <summary>
    /// Cancellation token to use for any asynchronous tasks awaited by the event.
    /// </summary>
    public CancellationToken Token { get; init; }

    internal CommandEventArgs(INetwork network, ICommand command, CancellationToken token)
    {
        Network = network;
        Command = command;
        Token = token;
    }
}
