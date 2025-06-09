// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.PluginFramework.Context;

namespace Netwolf.PluginFramework.Commands;

/// <summary>
/// Information surrounding a command.
/// </summary>
/// <param name="Command">Command received.</param>
/// <param name="PluginHost">The plugin host, used to interact with the network or client.</param>
/// <param name="Context">Opaque context object used to associate this command with a particular client or framework.</param>
/// <param name="CancellationToken">Cancellation token used to terminate async commands prematurely.</param>
public record PluginCommandEventArgs(
    ICommand Command,
    IPluginHost PluginHost,
    IPluginCommandContext Context,
    CancellationToken CancellationToken);
