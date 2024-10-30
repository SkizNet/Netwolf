// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Netwolf.PluginFramework.Commands;

/// <summary>
/// Type-erased <see cref="ICommandHandler{TResult}"/> for use in nongeneric contexts
/// </summary>
public interface ICommandHandler
{
    string Command { get; }

    string? Privilege => null;
}
