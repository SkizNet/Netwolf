// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.DependencyInjection;

namespace Netwolf.BotFramework.Internal;

internal sealed class BotBuilder : IBotBuilder
{
    public string BotName { get; init; }

    public IServiceCollection Services { get; init; }

    public BotBuilder(string botName, IServiceCollection services)
    {
        BotName = botName;
        Services = services;
    }
}
