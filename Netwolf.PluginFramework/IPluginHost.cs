// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.PluginFramework.Commands;

namespace Netwolf.PluginFramework;

/// <summary>
/// Exposes methods that a plugin can use to interact with its host application.
/// </summary>
/// <remarks>
/// This API surface is carefully designed to avoid exposing symbols from the plugin
/// assembly into the base AssemblyLoadContext. When that is unavoidable (e.g. callbacks),
/// they are maintained by the plugin host layer and not further exposed to the rest
/// of the framework so that we only need to look into/touch one place during an unload event.
/// </remarks>
public interface IPluginHost
{
    /// <summary>
    /// Hot observable of all commands received from the network. The plugin may subscribe
    /// to this in order to handle server commands.
    /// </summary>
    IObservable<PluginCommandEventArgs> ServerCommandStream { get; }

    /// <summary>
    /// Hook into a particular server command by running the specified callback when
    /// such a command is received.
    /// </summary>
    /// <param name="command">Command name to hook, case-insensitive</param>
    /// <param name="callback">Callback to run when command is received from the server</param>
    /// <returns>Hook instance that removes the hook once disposed</returns>
    IDisposable HookServer(string command, Func<PluginCommandEventArgs, Task> callback);

    /// <summary>
    /// Hook into a particular server command by running the specified callback with
    /// the specified plugin context when such a command is received.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="command">Command name to hook, case-insensitive</param>
    /// <param name="callback">Callback to run when command is received from the server</param>
    /// <param name="pluginContext">Context object to pass to the callback</param>
    /// <returns>Hook instance that removes the hook once disposed</returns>
    IDisposable HookServer<T>(string command, Func<PluginCommandEventArgs, T, Task> callback, T pluginContext);

    /// <summary>
    /// Hook into a command issued by the client (according to the framework in use).
    /// In general, it is better to decorate commands with CommandAttribute, as it provides
    /// a more ergonomic API for parameter input. However, this API allows for later removal
    /// of the command whereas commands defined via CommandAttribute are defined for the
    /// lifetime of the plugin. Additionally, unlike CommandAttribute, a hook can be defined
    /// for a command that already exists, and the hook can "pre-empt" the existing command.
    /// </summary>
    /// <param name="command">Command name to hook, case-insensitive</param>
    /// <param name="callback">Callback to run when command is executed by the framework</param>
    /// <returns>Hook instance that removes the hook once disposed</returns>
    IDisposable HookCommand(string command, Func<PluginCommandEventArgs, Task<PluginResult>> callback);

    /// <summary>
    /// Hook into a command issued by the client (according to the framework in use).
    /// In general, it is better to decorate commands with CommandAttribute, as it provides
    /// a more ergonomic API for parameter input. However, this API allows for later removal
    /// of the command whereas commands defined via CommandAttribute are defined for the
    /// lifetime of the plugin. Additionally, unlike CommandAttribute, a hook can be defined
    /// for a command that already exists, and the hook can "pre-empt" the existing command.
    /// </summary>
    /// <param name="command">Command name to hook, case-insensitive</param>
    /// <param name="callback">Callback to run when command is executed by the framework</param>
    /// <param name="pluginContext">Context object to pass to the callback</param>
    /// <returns>Hook instance that removes the hook once disposed</returns>
    IDisposable HookCommand<T>(string command, Func<PluginCommandEventArgs, T, Task<PluginResult>> callback, T pluginContext);

    /// <summary>
    /// Schedule a callback to run on a set frequency. If the callback is already running
    /// when the timer elapses again, it will be skipped. The callback will otherwise be
    /// executed each time the frequency elapses until the hook is disposed.
    /// </summary>
    /// <param name="frequency">Frequency of timer executions</param>
    /// <param name="callback">Callback to run each time the frequency elapses</param>
    /// <returns>Hook instance that removes the hook once disposed</returns>
    IDisposable HookTimer(TimeSpan frequency, Func<PluginTimerEventArgs, Task> callback);

    /// <summary>
    /// Schedule a callback to run on a set frequency. If the callback is already running
    /// when the timer elapses again, it will be skipped. The callback will otherwise be
    /// executed with the provided context each time the frequency elapses until the hook is disposed.
    /// </summary>
    /// <param name="frequency">Frequency of timer executions</param>
    /// <param name="callback">Callback to run each time the frequency elapses</param>
    /// <param name="pluginContext">Context object to pass to the callback</param>
    /// <returns>Hook instance that removes the hook once disposed</returns>
    IDisposable HookTimer<T>(TimeSpan frequency, Func<PluginTimerEventArgs, T, Task> callback, T pluginContext);
}
