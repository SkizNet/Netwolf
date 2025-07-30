// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.Extensions.Logging;

using Netwolf.Transport.Events;
using Netwolf.Transport.IRC;

namespace Netwolf.Transport.Internal.CommandListeners;

[CommandListener]
internal class Error : ICommandListener
{
    private ILogger<INetwork> Logger { get; init; }

    public IReadOnlyCollection<string> CommandFilter => ["ERROR"];

    public Error(ILogger<INetwork> logger)
    {
        Logger = logger;
    }

    public void Execute(CommandEventArgs args)
    {
        // ERROR :<reason>
        Logger.LogInformation("Received an ERROR from the server: {Reason}", args.Command.Args[0]);
    }
}
