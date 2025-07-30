// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.Events;

namespace Netwolf.Transport.Internal;

/// <summary>
/// Interface for internal command listeners. Listeners must additionally
/// use the CommandListenerAttribute in order to be picked up by the source generator
/// and automatically registered with CommandListenerRegistry.
/// </summary>
internal interface IAsyncCommandListener
{
    /// <summary>
    /// Commands that this listener uses as a base filter.
    /// If the Execute method makes use of DeferredCommands, those are attached
    /// as separate subscriptions to the CommandReceived observable and will not
    /// be subject to this filter.
    /// </summary>
    IReadOnlyCollection<string> CommandFilter { get; }

    /// <summary>
    /// Callback to execute when a command matching the filter is received.
    /// The cancellation token in CommandEventArgs should be used to check for cancellation.
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    Task ExecuteAsync(CommandEventArgs args);
}
