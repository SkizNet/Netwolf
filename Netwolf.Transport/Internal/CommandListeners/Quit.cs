// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.Extensions.Logging;

using Netwolf.Transport.Events;
using Netwolf.Transport.IRC;

namespace Netwolf.Transport.Internal.CommandListeners;

[CommandListener]
internal class Quit : ICommandListener
{
    private ILogger<INetwork> Logger { get; init; }

    public IReadOnlyCollection<string> CommandFilter => ["QUIT"];

    public Quit(ILogger<INetwork> logger)
    {
        Logger = logger;
    }

    public void Execute(CommandEventArgs args)
    {
        // QUIT [:<reason>]
        var info = args.Network.AsNetworkInfo();

        if (!IrcUtil.TryExtractUserFromSource(args.Command, info, out var user))
        {
            return;
        }

        // spec says if the client quits the server replies with ERROR, not QUIT
        if (user.Id == info.ClientId)
        {
            Logger.LogWarning("Protocol violation: Received a QUIT message with our client as its source");
            return;
        }

        // Purge user from state
        args.Network.UnsafeUpdateUser(user with { Channels = user.Channels.Clear() });
    }
}
