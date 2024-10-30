﻿// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Netwolf.PluginFramework.Commands;

namespace Netwolf.PluginFramework.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPluginFrameworkServices(this IServiceCollection services)
    {
        services.TryAddScoped(typeof(ICommandDispatcher<>), typeof(CommandDispatcher<>));
        services.TryAddSingleton(typeof(ICommandValidator<>), typeof(CommandValidator<>));

        return services;
    }
}
