﻿// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.Extensions.DependencyInjection;

using Netwolf.Transport.Extensions;
using Netwolf.Transport.Internal;
using Netwolf.Transport.IRC;

using System.Reactive.Linq;

namespace Netwolf.Transport.Commands;

public partial class CommandListenerRegistry
{
    internal IReadOnlyCollection<ICommandListener> SyncListeners { get; init; }

    internal IReadOnlyCollection<IAsyncCommandListener> AsyncListeners { get; init; }

    public CommandListenerRegistry(IServiceProvider provider)
    {
        var listeners = GetCommandListenerTypes()
            .Select(type => ActivatorUtilities.CreateInstance(provider, type))
            .ToList();

        SyncListeners = listeners.OfType<ICommandListener>().ToList();
        AsyncListeners = listeners.OfType<IAsyncCommandListener>().ToList();
    }

    public void RegisterForNetwork(INetwork network, CancellationToken cancellationToken)
    {
        foreach (var listener in AsyncListeners)
        {
            network.CommandReceived
                .Where(args => listener.CommandFilter.Contains(args.Command.Verb))
                .SubscribeAsync(listener.ExecuteAsync, cancellationToken);
        }

        foreach (var listener in SyncListeners)
        {
            network.CommandReceived
                .Where(args => listener.CommandFilter.Contains(args.Command.Verb))
                .Subscribe(listener.Execute, cancellationToken);
        }
    }

    private static partial IEnumerable<Type> GetCommandListenerTypes();
}
