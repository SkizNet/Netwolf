// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.Commands;
using Netwolf.Transport.Events;

namespace Netwolf.Transport.Internal.CommandListeners;

[CommandListener]
internal class Ping : IAsyncCommandListener
{
    private ICommandFactory CommandFactory { get; init; }

    public IReadOnlyCollection<string> CommandFilter => ["PING"];

    public Ping(ICommandFactory commandFactory)
    {
        CommandFactory = commandFactory;
    }

    public async Task ExecuteAsync(CommandEventArgs args)
    {
        // PING <token>
        // although older ircds have different meanings and possibly a second arg
        // As such we repeat all args we receive back in the PONG response.

        var info = args.Network.AsNetworkInfo();
        var pong = CommandFactory.PrepareClientCommand(
            info.Self,
            "PONG",
            args.Command.Args,
            null,
            CommandCreationOptions.MakeOptions(info));

        await args.Network.SendAsync(pong, args.Token);
    }
}
