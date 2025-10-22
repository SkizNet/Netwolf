// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Netwolf.Transport.Extensions;
using Netwolf.Transport.Internal;
using Netwolf.Transport.IRC;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Netwolf.Transport.Commands;

public partial class CommandListenerRegistry : IDisposable
{
    private readonly Lazy<List<ICommandListener>> _syncListeners;
    private readonly Lazy<List<IAsyncCommandListener>> _asyncListeners;
    private bool _disposed = false;
    private readonly EventLoopScheduler _scheduler;
    private readonly Subject<object> _subject = new();

    private IServiceProvider ServiceProvider { get; init; }

    private ILogger<CommandListenerRegistry> Logger { get; init; }

    private IReadOnlyCollection<ICommandListener> SyncListeners => _syncListeners.Value;

    private IReadOnlyCollection<IAsyncCommandListener> AsyncListeners => _asyncListeners.Value;

    /// <summary>
    /// Message passing interface for listeners to subscribe to events that the Network can listen to.
    /// This is intended for use by the INetwork instance itself; events that other subscribers
    /// would be interested in are emitted by events directly on INetwork.
    /// </summary>
    public IObservable<object> CommandListenerEvents => _subject.ObserveOn(_scheduler);

    public CommandListenerRegistry(IServiceProvider provider, ILogger<CommandListenerRegistry> logger)
    {
        ServiceProvider = provider;
        Logger = logger;

        _syncListeners = new(Initialize<ICommandListener>);
        _asyncListeners = new(Initialize<IAsyncCommandListener>);
        _scheduler = new(MakeCommandListenerEventLoop);
    }

    /// <summary>
    /// Emits the specified event to all subscribed observers.
    /// </summary>
    /// <param name="evt">The event to emit. Cannot be <see langword="null"/>.</param>
    public void EmitEvent(object evt)
    {
        ArgumentNullException.ThrowIfNull(evt);
        _subject.OnNext(evt);
    }

    private Thread MakeCommandListenerEventLoop(ThreadStart start)
    {
        return new(() =>
        {
            try
            {
                start.Invoke();
            }
            catch (Exception ex)
            {
                // log exceptions but do not abort the network connection
                // (because this is a Singleton service we don't even know which network this is for at this point)
                Logger.LogError(ex, "An unhandled exception occurred in a command listener.");
            }
        });
    }

    /// <summary>
    /// Lazy initialization for the listener lists, since a listener may take this service as a dependency.
    /// As a result, we need this service to be fully constructed before we can construct the listeners.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    [SuppressMessage("Style", "IDE0305:Simplify collection initialization",
        Justification = "ToList() produces cleaner-looking code than a collection expression here")]
    private List<T> Initialize<T>()
    {
        var list = GetCommandListenerTypes()
            .Where(type => typeof(T).IsAssignableFrom(type))
            .Select(type => ActivatorUtilities.CreateInstance(ServiceProvider, type))
            .Cast<T>()
            .ToList();

        foreach (var listener in list)
        {
            if (listener is IAsyncCommandListener asyncListener)
            {
                Logger.LogInformation(
                    "Found {Listener} for commands {Command}",
                    listener.GetType().FullName,
                    string.Join(", ", asyncListener.CommandFilter));
            }

            if (listener is ICommandListener syncListener)
            {
                Logger.LogInformation(
                    "Found {Listener} for commands {Command}",
                    listener.GetType().FullName,
                    string.Join(", ", syncListener.CommandFilter));
            }
        }

        return list;
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

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                if (_syncListeners.IsValueCreated)
                {
                    foreach (var listener in _syncListeners.Value)
                    {
                        if (listener is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                    }
                }

                if (_asyncListeners.IsValueCreated)
                {
                    foreach (var listener in _asyncListeners.Value)
                    {
                        if (listener is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                    }
                }

                _subject.Dispose();
                _scheduler.Dispose();
            }

            _disposed = true;
        }
    }

    void IDisposable.Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
