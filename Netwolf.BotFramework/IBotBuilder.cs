// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.DependencyInjection;

namespace Netwolf.BotFramework;

/// <summary>
/// Configuration builder for a Bot
/// </summary>
public interface IBotBuilder
{
    /// <summary>
    /// Bot name; also used as service key
    /// </summary>
    string BotName { get; }

    /// <summary>
    /// The service collection in use
    /// </summary>
    IServiceCollection Services { get; }
}
