// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.Commands;
using Netwolf.Transport.Context;

using System.Collections.Immutable;

namespace Netwolf.PluginFramework.Commands;

/// <summary>
/// Service interface for a command dispatcher.
/// </summary>
/// <typeparam name="TResult"></typeparam>
public interface ICommandDispatcher<TResult> : ICommandDispatcher
{
    ImmutableArray<string> Commands { get; }

    Task<TResult?> DispatchAsync(ICommand command, IContext sender, CancellationToken cancellationToken);

    ICommandHandler<TResult>? AddCommand<TCommand>()
        where TCommand : ICommandHandler<TResult>;

    bool AddCommand(ICommandHandler<TResult> handler);
}
