// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.Commands;

using System.Reflection;

namespace Netwolf.PluginFramework.Commands;

/// <summary>
/// Type-erased interface for a command dispatcher.
/// This interface only supports the addition/removal of commands;
/// it cannot execute them.
/// </summary>
public interface ICommandDispatcher
{
    void AddCommandsFromAssembly(Assembly assembly);

    ICommandHandler? AddCommand(Type commandType);

    bool RemoveCommand(ICommandHandler handler);
}
