// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.Extensions.Logging;

using Netwolf.Transport.Events;
using Netwolf.Transport.IRC;
using Netwolf.Transport.State;

namespace Netwolf.Transport.Internal.CommandListeners;

[CommandListener]
internal class Mode : ICommandListener
{
    private ILogger<INetwork> Logger { get; init; }

    public IReadOnlyCollection<string> CommandFilter => ["MODE"];

    public Mode(ILogger<INetwork> logger)
    {
        Logger = logger;
    }

    public void Execute(CommandEventArgs args)
    {
        // MODE <target> [<modestring> [<mode arguments>...]]
        var info = args.Network.AsNetworkInfo();
        var command = args.Command;

        // only care if we're changing our own modes or channel modes
        bool adding = true;

        // for user modes, we may receive a MODE message before we officially mark us as "connected"
        // (i.e. before we complete a WHO on ourselves)
        if (info.GetUserByNick(command.Args[0]) is UserRecord user && user.Id == info.ClientId)
        {
            List<char> toAdd = [];
            List<char> toRemove = [];
            foreach (var c in command.Args[1])
            {
                switch (c)
                {
                    case '+':
                        adding = true;
                        break;
                    case '-':
                        adding = false;
                        break;
                    default:
                        (adding ? toAdd : toRemove).Add(c);
                        break;
                }
            }

            args.Network.UnsafeUpdateUser(user with { Modes = user.Modes.Union(toAdd).Except(toRemove) });
        }
        else if (info.GetChannel(command.Args[0]) is ChannelRecord channel)
        {
            // take a snapshot of the various mode types since calling the underlying properties recomputes the value each time.
            string prefixModes = info.ChannelPrefixModes;
            string prefixSymbols = info.ChannelPrefixSymbols;
            string typeAModes = info.ChannelModesA;
            string typeBModes = info.ChannelModesB;
            string typeCModes = info.ChannelModesC;
            string typeDModes = info.ChannelModesD;

            // index of the next mode argument
            int argIndex = 2;
            var changed = channel.Modes;

            foreach (var c in command.Args[1])
            {
                switch (c)
                {
                    case '+':
                        adding = true;
                        break;
                    case '-':
                        adding = false;
                        break;
                    case var _ when prefixModes.Contains(c):
                        {
                            var target = info.GetUserByNick(command.Args[argIndex]);
                            if (target == null || !target.Channels.TryGetValue(channel.Id, out string? status))
                            {
                                Logger.LogWarning(
                                    "Potential state corruption detected: Received MODE message for {Nick} on {Channel} but they do not exist in state",
                                    command.Args[argIndex],
                                    channel.Name);

                                break;
                            }

                            argIndex++;
                            var statusSet = new HashSet<char>(status);
                            var symbol = prefixSymbols[prefixModes.IndexOf(c)];
                            if (adding)
                            {
                                statusSet.Add(symbol);
                            }
                            else
                            {
                                statusSet.Remove(symbol);
                            }

                            status = string.Concat(prefixSymbols.Where(statusSet.Contains));

                            // keep channel updated for future loop iterations
                            channel = channel with { Users = channel.Users.SetItem(target.Id, status) };
                        }
                        break;
                    case var _ when typeAModes.Contains(c):
                        // we don't track list modes but we still need to advance arg index
                        argIndex++;
                        break;
                    case var _ when typeBModes.Contains(c):
                        if (adding)
                        {
                            changed = changed.SetItem(c, command.Args[argIndex]);
                        }
                        else
                        {
                            changed = changed.Remove(c);
                        }

                        argIndex++;
                        break;
                    case var _ when typeCModes.Contains(c):
                        if (adding)
                        {
                            changed = changed.SetItem(c, command.Args[argIndex]);
                            argIndex++;
                        }
                        else
                        {
                            changed = changed.Remove(c);
                        }
                        break;
                    case var _ when typeDModes.Contains(c):
                        if (adding)
                        {
                            changed = changed.SetItem(c, null);
                        }
                        else
                        {
                            changed = changed.Remove(c);
                        }
                        break;
                    default:
                        // hope it's a mode without an argument as otherwise this will mess everything else up
                        Logger.LogWarning("Protocol violation: Received MODE command for unknown mode letter {Mode}", c);
                        break;
                }
            }

            args.Network.UnsafeUpdateChannel(channel with { Modes = changed });
        }
    }
}
