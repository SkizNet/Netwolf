// Copyright(c) 2025 Ryan Schmidt<skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.IRC;

namespace Netwolf.PluginFramework.Commands;

/// <summary>
/// Service that allows plugins to hook into commands provided by various downstream frameworks.
/// </summary>
public interface ICommandHookRegistry
{
    /// <summary>
    /// Register a command hook with the specified handler at the specified priority.
    /// </summary>
    /// <param name="handler">Hook handler</param>
    /// <param name="priority"></param>
    /// <returns>An <see cref="IDisposable"/> instance that, when disposed, will remove the hook.</returns>
    /// <remarks>
    /// The command being hooked does not need to exist as a built-in command. When hooking a command that does not exist,
    /// it is a good idea to return <see cref="PluginResult.SuppressDefault"/> to prevent warnings about the command not being found.
    /// No permission checks are performed on commands where hooks exist but no built-in command exists; all users will be able to execute the command.
    /// If you need to restrict command execution, prefer to register as a built-in command rather than as a command hook.
    /// </remarks>
    IDisposable AddCommandHook(ICommandHandler<PluginResult> handler, CommandHookPriority priority = CommandHookPriority.Normal);

    /// <summary>
    /// Retrieve all hooks for the given command, ordered from highest to lowest priority.
    /// The returned enumerable is a point-in-time snapshot and will not reflect later additions or removals to the hook list.
    /// </summary>
    /// <param name="command">Command name, normalized to all-uppercase</param>
    /// <returns></returns>
    IEnumerable<ICommandHandler<PluginResult>> GetCommandHooks(string command);

    /// <summary>
    /// Retrieve all commands that have hooks defined for them, in no particular order.
    /// The returned enumerable is a point-in-time snapshot and will not reflect later additions or removals to the hook list.
    /// </summary>
    /// <returns></returns>
    IEnumerable<string> GetHookedCommandNames();
}
