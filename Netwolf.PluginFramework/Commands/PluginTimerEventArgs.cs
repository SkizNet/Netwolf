// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.PluginFramework.Commands;

/// <summary>
/// Information surrounding a timer.
/// </summary>
/// <param name="PluginHost">The plugin host, used to interact with the network or client.</param>
/// <param name="CancellationToken">Cancellation token used to terminate async commands prematurely.</param>
public record PluginTimerEventArgs(
    IPluginHost PluginHost,
    CancellationToken CancellationToken);
