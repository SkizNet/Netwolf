// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.IRC;
using Netwolf.Transport.State;

using System.Collections.Immutable;

namespace Netwolf.Transport.Context;

/// <summary>
/// Partial implemenation of <see cref="IContext"/> that provides extension data storage and management.
/// Custom context implementations should extend this class to avoid boilerplate code.
/// <para/>
/// However, consumers of context should continue to use the <see cref="IContext"/> interface directly
/// as custom implementations are not required to make use of this base class.
/// </summary>
public abstract class ExtensibleContextBase : IContext
{
    private bool _frozen = false;
    private ImmutableDictionary<Type, object?> _extensionData = ImmutableDictionary<Type, object?>.Empty;

    public abstract object Sender { get; }
    public abstract INetworkInfo Network { get; }
    public abstract ChannelRecord? Channel { get; }
    public abstract UserRecord? User { get; }

    public IValidationContextFactory? ValidationContextFactory { get; protected init; }

    public void ClearExtensionData<T>()
        where T : class
    {
        if (_frozen)
        {
            throw new InvalidOperationException("Extension data is frozen and cannot be modified.");
        }

        _extensionData = _extensionData.Remove(typeof(T));
    }

    public void FreezeExtensionData()
    {
        _frozen = true;
    }

    public T? GetExtensionData<T>()
        where T : class
    {
        if (_extensionData.TryGetValue(typeof(T), out var value))
        {
            return value as T;
        }

        throw new KeyNotFoundException($"No extension data for type {typeof(T).FullName} is defined for this context.");
    }

    public T? GetExtensionData<T>(T? defaultValue)
        where T : class
    {
        return _extensionData.GetValueOrDefault(typeof(T), defaultValue) as T;
    }

    public bool HasExtensionData<T>()
        where T : class
    {
        return _extensionData.ContainsKey(typeof(T));
    }

    public void SetExtensionData<T>(T? value)
        where T : class
    {
        if (_frozen)
        {
            throw new InvalidOperationException("Extension data is frozen and cannot be modified.");
        }

        _extensionData = _extensionData.SetItem(typeof(T), value);
    }
}
