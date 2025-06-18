// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.Commands;
using Netwolf.Transport.IRC;

namespace Netwolf.Transport.Context;

public class ServerCommandContext : ExtensibleContextBase
{
    public override object Sender => Network;

    public INetwork Network { get; init; }

    public ICommand Command { get; init; }

    public ServerCommandContext(INetwork network, ICommand command)
    {
        Network = network;
        Command = command;
    }
}
