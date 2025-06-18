// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.PluginFramework.Context;
using Netwolf.Transport.Commands;
using Netwolf.Transport.Context;

namespace Netwolf.PluginFramework.Commands;

internal class PluginCommandHookHandler : ICommandHandler<PluginResult>
{
    public string Command { get; init; }

    private IPluginHost PluginHost { get; init; }

    private Func<PluginCommandEventArgs, Task<PluginResult>> Callback { get; init; }

    internal PluginCommandHookHandler(IPluginHost pluginHost, string command, Func<PluginCommandEventArgs, Task<PluginResult>> callback)
    {
        PluginHost = pluginHost;
        Command = command;
        Callback = callback;
    }

    public async Task<PluginResult> ExecuteAsync(ICommand command, IContext sender, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PluginCommandContext context = new(sender);
        return await Callback(new(command, PluginHost, context, cancellationToken));
    }
}
