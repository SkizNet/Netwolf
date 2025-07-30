// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.Extensions.Logging;

using Netwolf.Transport.Events;
using Netwolf.Transport.IRC;
using Netwolf.Transport.State;

using System.Text.RegularExpressions;

namespace Netwolf.Transport.Internal.CommandListeners;

[CommandListener]
internal partial class UserHost : ICommandListener
{
    [GeneratedRegex(@"^(?<nick>[^*=]+)(?<isop>\*)?=(?<isaway>[+-])(?<hostname>.+)$")]
    private static partial Regex ReplyRegex();

    private ILogger<INetwork> Logger { get; init; }

    public IReadOnlyCollection<string> CommandFilter => ["302"];

    public UserHost(ILogger<INetwork> logger)
    {
        Logger = logger;
    }

    public void Execute(CommandEventArgs args)
    {
        // RPL_USERHOST <client> :[<reply>{ <reply>}]
        // reply = nickname [ isop ] "=" isaway hostname
        // isop = "*"
        // isaway = ("+" / "-") -- "+" means here, "-" means away
        // Note: operator status is ignored

        var info = args.Network.AsNetworkInfo();
        foreach (var reply in args.Command.Args[1].Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var match = ReplyRegex().Match(reply);
            if (!match.Success)
            {
                Logger.LogWarning("Protocol violation: RPL_USERHOST reply {Reply} does not match expected format", reply);
                continue;
            }

            if (info.GetUserByNick(match.Groups["nick"].Value) is UserRecord user)
            {
                args.Network.UnsafeUpdateUser(user with
                {
                    IsAway = match.Groups["isaway"].Value == "-",
                    Host = match.Groups["hostname"].Value,
                });
            }
        }
    }
}
