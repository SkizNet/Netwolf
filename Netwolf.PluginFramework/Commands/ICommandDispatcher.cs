﻿// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.PluginFramework.Context;

using System.Collections.Immutable;
using System.Reflection;

namespace Netwolf.PluginFramework.Commands;

/// <summary>
/// Service interface for a command dispatcher.
/// </summary>
/// <typeparam name="TResult"></typeparam>
public interface ICommandDispatcher<TResult>
{
    ImmutableArray<string> Commands { get; }

    Task<TResult?> DispatchAsync(ICommand command, IContext sender, CancellationToken cancellationToken);

    void AddCommandsFromAssembly(Assembly assembly);

    void AddCommand<TCommand>()
        where TCommand : ICommandHandler<TResult>;

    void AddCommand(Type commandType);

    void AddCommand(ICommandHandler<TResult> handler);
}
