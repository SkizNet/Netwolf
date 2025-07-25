// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.Commands;
using Netwolf.Transport.IRC;
using Netwolf.Transport.State;

namespace Netwolf.Transport.Context;

public class ServerCommandContext : ExtensibleContextBase
{
    private readonly INetwork _network;
    private readonly INetworkInfo _networkInfo;

    public override object Sender => _network;
    protected override INetworkInfo GetContextNetwork() => _networkInfo;
    protected override ChannelRecord? GetContextChannel() => null;
    protected override UserRecord? GetContextUser() => null;

    public ICommand Command { get; init; }

    public ServerCommandContext(INetwork network, ICommand command)
    {
        _network = network;
        _networkInfo = network.AsNetworkInfo();
        Command = command;
        // TODO: Process Command to extract channel/user context
    }
}
