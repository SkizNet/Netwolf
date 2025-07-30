// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.Extensions.Logging;

using Netwolf.Transport.Events;
using Netwolf.Transport.IRC;
using Netwolf.Transport.State;

namespace Netwolf.Transport.Internal.CommandListeners;

[CommandListener]
internal class Part : ICommandListener
{
    private ILogger<INetwork> Logger { get; init; }

    public IReadOnlyCollection<string> CommandFilter => ["PART"];

    public Part(ILogger<INetwork> logger)
    {
        Logger = logger;
    }

    public void Execute(CommandEventArgs args)
    {
        // PART <channel>{,<channel>} [:<reason>]
        var info = args.Network.AsNetworkInfo();
        if (!IrcUtil.TryExtractUserFromSource(args.Command, info, out var user))
        {
            return;
        }

        // RFC states that the PART message from server to client SHOULD NOT send multiple channels, not MUST NOT, so accomodate multiple channels here
        foreach (var channelName in args.Command.Args[0].Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (info.GetChannel(channelName) is not ChannelRecord channel)
            {
                Logger.LogWarning("Potential state corruption detected: Received PART message for {Channel} but it does not exist in state", channelName);
                continue;
            }

            args.Network.UnsafeUpdateChannel(channel with
            {
                Users = channel.Users.Remove(user.Id)
            });
        }
    }
}
