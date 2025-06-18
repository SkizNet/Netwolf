// Copyright(c) 2025 Ryan Schmidt<skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.Extensions.Logging;

using Netwolf.Transport.Commands;

using System.Collections.Concurrent;
using System.Reactive.Disposables;

namespace Netwolf.PluginFramework.Commands;

internal sealed class CommandHookRegistry : ICommandHookRegistry
{
    private record CommandHookRecord(ICommandHandler<PluginResult> Handler, int Priority);

    private int _discriminator = 0;
    private readonly ConcurrentDictionary<int, CommandHookRecord> _hooks = [];

    private ILogger<ICommandHookRegistry> Logger { get; init; }

    public CommandHookRegistry(ILogger<ICommandHookRegistry> logger)
    {
        Logger = logger;
    }

    public IDisposable AddCommandHook(ICommandHandler<PluginResult> handler, CommandHookPriority priority = CommandHookPriority.Normal)
    {
        int discriminator = Interlocked.Increment(ref _discriminator);
        int calculatedPriority = (int)priority * 1000000 + discriminator;

        CommandHookRecord entry = new(handler, calculatedPriority);
        if (!_hooks.TryAdd(calculatedPriority, entry))
        {
            throw new InvalidOperationException("Too many command hooks have been registered");
        }

        Logger.LogDebug("Added hook {FullName} for command {Command} at priority {Priority}", handler.UnderlyingFullName, handler.Command, priority);
        return Disposable.Create(() => RemoveCommandHook(calculatedPriority));
    }

    private void RemoveCommandHook(int priority)
    {
        if (_hooks.TryRemove(priority, out var entry))
        {
            Logger.LogDebug("Removed hook {FullName} for command {Command}", entry.Handler.UnderlyingFullName, entry.Handler.Command);
        }
    }

    public IEnumerable<ICommandHandler<PluginResult>> GetCommandHooks(string command)
    {
        // Ensure the snapshot represents the point in time that this method is called,
        // rather than when the caller starts to enumerate over the results.
        var valuesSnapshot = _hooks.Values;

        return from entry in valuesSnapshot
               where entry.Handler.Command == command
               orderby entry.Priority
               select entry.Handler;
    }

    public IEnumerable<string> GetHookedCommandNames()
    {
        // Ensure the snapshot represents the point in time that this method is called,
        // rather than when the caller starts to enumerate over the results.
        var valuesSnapshot = _hooks.Values;

        return valuesSnapshot.Select(entry => entry.Handler.Command).Distinct();
    }
}
