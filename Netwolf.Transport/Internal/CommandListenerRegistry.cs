// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.Extensions.DependencyInjection;

namespace Netwolf.Transport.Internal;

internal partial class CommandListenerRegistry
{
    internal IReadOnlyCollection<ICommandListener> Listeners { get; init; }

    public CommandListenerRegistry(IServiceProvider provider)
    {
        Listeners = GetCommandListenerTypes()
            .Select(type => ActivatorUtilities.CreateInstance(provider, type))
            .OfType<ICommandListener>()
            .ToList();
    }

    private static partial IEnumerable<Type> GetCommandListenerTypes();
}
