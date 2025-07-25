// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.PluginFramework.Commands;
using Netwolf.PluginFramework.Context;
using Netwolf.Transport.Context;
using Netwolf.Transport.Events;
using Netwolf.Transport.IRC;

using System.Collections.Concurrent;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Netwolf.PluginFramework.Loader;

/// <summary>
/// Shim class between a plugin and the application.
/// IPluginHost defines the public API that a plugin is able to use to perform interactions.
/// This class *must not* hold a reference to the IPlugin itself in any long-duration lifetime,
/// as otherwise it may become impossible to unload/collect plugins (= memory leaks).
/// </summary>
internal sealed class PluginHost : IPluginHost, IDisposable
{
    private readonly TimeSpan FIFTY_MS = TimeSpan.FromMilliseconds(50);

    private bool _disposed = false;

    public event EventHandler? Unloading;

    private int PluginId { get; init; }

    private WeakReference<IPlugin> Plugin { get; init; }

    private ICommandHookRegistry HookRegistry { get; init; }

    private NetworkEvents NetworkEvents { get; init; }

    private ConcurrentBag<IDisposable> Hooks { get; init; } = [];

    private Dictionary<INetwork, IDisposable> Subscriptions { get; init; } = [];

    /// <summary>
    /// Subject that plugin hooks will subscribe to. This Subject will itself be subscribed to the
    /// command streams of networks as they are connected (and unsubscribed as they are disconnected).
    /// By proxying through a subject, we not only multiplex multiple networks into a single stream, but
    /// also gain the ability to call Subject.Dispose during unload to force-unload all plugin subscriptions
    /// without requiring cooperation from third-party plugin code.
    /// </summary>
    private Subject<PluginCommandEventArgs> PluginCommandStream { get; init; } = new();

    /// <summary>
    /// CTS used to cancel pending plugin operations when an unload is requested.
    /// </summary>
    private CancellationTokenSource CancellationSource { get; init; } = new();

    public IObservable<PluginCommandEventArgs> ServerCommandStream
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return PluginCommandStream.AsObservable();
        }
    }

    public PluginHost(
        ICommandHookRegistry hookRegistry,
        NetworkEvents networkEvents,
        IPlugin plugin,
        int pluginId)
    {
        HookRegistry = hookRegistry;
        NetworkEvents = networkEvents;
        Plugin = new(plugin);
        PluginId = pluginId;

        NetworkEvents.NetworkConnected += OnNetworkConnected;
        NetworkEvents.NetworkDisconnected += OnNetworkDisconnected;
    }

    public IDisposable HookServer(string command, Func<PluginCommandEventArgs, Task> callback)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var hook = ServerCommandStream
            .Where(args => args.Command.Verb.Equals(command, StringComparison.OrdinalIgnoreCase))
            .Select(args => Observable.FromAsync(() => callback(args)))
            .Concat()
            .Subscribe();

        Hooks.Add(hook);
        return hook;
    }

    public IDisposable HookServer<T>(string command, Func<PluginCommandEventArgs, T, Task> callback, T pluginData)
    {
        return HookServer(command, args => callback(args, pluginData));
    }

    public IDisposable HookCommand(string command, Func<PluginCommandEventArgs, Task<PluginResult>> callback)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var handler = new PluginCommandHookHandler(this, command.ToUpperInvariant(), callback);
        var hook = HookRegistry.AddCommandHook(handler);
        Hooks.Add(hook);
        return hook;
    }

    public IDisposable HookCommand<T>(string command, Func<PluginCommandEventArgs, T, Task<PluginResult>> callback, T pluginData)
    {
        return HookCommand(command, args => callback(args, pluginData));
    }

    public IDisposable HookTimer(TimeSpan frequency, Func<PluginTimerEventArgs, Task> callback)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (frequency < FIFTY_MS)
        {
            frequency = FIFTY_MS;
        }

        int timerLock = 0;
        PluginTimerEventArgs args = new(this, CancellationSource.Token);

        var subscription = Observable
            .Interval(frequency)
            .SelectMany(i =>
            {
                if (Interlocked.CompareExchange(ref timerLock, 1, 0) == 0)
                {
                    return Observable.FromAsync(() => callback(args)).Finally(() => Volatile.Write(ref timerLock, 0));
                }
                else
                {
                    return Observable.Empty<Unit>();
                }
            })
            .Subscribe();

        Hooks.Add(subscription);
        return subscription;
    }

    public IDisposable HookTimer<T>(TimeSpan frequency, Func<PluginTimerEventArgs, T, Task> callback, T pluginData)
    {
        return HookTimer(frequency, args => callback(args, pluginData));
    }

    private void OnNetworkConnected(object? sender, NetworkEventArgs e)
    {
        // only emit OnNext, we don't want to send OnError/OnCompleted along to plugin listeners
        var subscription = e.Network.CommandReceived
            .Select(c => new PluginCommandEventArgs(
                c.Command,
                this,
                new PluginContext(new ServerCommandContext(c.Network, c.Command)),
                c.Token))
            .Subscribe(PluginCommandStream.OnNext);

        Subscriptions[e.Network] = subscription;
    }

    private void OnNetworkDisconnected(object? sender, NetworkEventArgs e)
    {
        if (Subscriptions.Remove(e.Network, out var subscription))
        {
            subscription.Dispose();
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            try
            {
                CancellationSource.Cancel(false);
            }
            catch (Exception)
            {
                // swallow exceptions here
            }

            while (Hooks.TryTake(out var hook))
            {
                hook.Dispose();
            }

            PluginCommandStream.Dispose();
            CancellationSource.Dispose();
            Unloading?.Invoke(this, EventArgs.Empty);
        }
    }
}
