// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.PluginFramework.Context;

namespace Netwolf.PluginFramework.Commands;

/// <summary>
/// Interface for a command handler.
/// Plugin command classes must implement this type with an appropriate <typeparamref name="TResult"/> specified by the
/// framework in use. Reflection is used to scan all classes implementing this interface to generate a command listing
/// from that plugin.
/// </summary>
/// <typeparam name="TResult"></typeparam>
public interface ICommandHandler<TResult> : ICommandHandler
{
    string UnderlyingFullName => GetType().FullName ?? "<unknown>";

    Task<TResult> ExecuteAsync(ICommand command, IContext sender, CancellationToken cancellationToken);
}
