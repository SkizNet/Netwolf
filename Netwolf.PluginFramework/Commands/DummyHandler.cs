// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.Extensions.Logging;

using Netwolf.Transport.IRC;

namespace Netwolf.PluginFramework.Commands;

internal class DummyHandler<TResult> : ICommandHandler<TResult>
{
    private ILogger? Logger { get; init; }

    public string Command { get; init; }

    internal DummyHandler(string command, ILogger? logger)
    {
        Command = command;
        Logger = logger;
    }

    public Task<TResult> ExecuteAsync(ICommand command, IContext sender, CancellationToken cancellationToken)
    {
        Logger?.LogDebug("Received unknown command {Command}", command.UnprefixedCommandPart);
        return Task.FromResult<TResult>(default!);
    }
}
