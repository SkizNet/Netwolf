// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Netwolf.PluginFramework.Commands;
using Netwolf.PluginFramework.Context;
using Netwolf.PluginFramework.Loader;

namespace Netwolf.PluginFramework.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPluginFrameworkServices(this IServiceCollection services)
    {
        services.TryAddSingleton<IPluginLoader, PluginLoader>();
        services.TryAddScoped(typeof(ICommandDispatcher<>), typeof(CommandDispatcher<>));
        services.TryAddSingleton(typeof(ICommandValidator<>), typeof(CommandValidator<>));
        services.TryAddScoped<IValidationContextFactory, ValidationContextFactory>();

        return services;
    }
}
