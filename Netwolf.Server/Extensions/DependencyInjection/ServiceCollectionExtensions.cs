﻿using Microsoft.Extensions.DependencyInjection;

using Netwolf.Server.Commands;

namespace Netwolf.Server.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddServerServices(this IServiceCollection services)
    {
        services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
        services.AddScoped<Network>();

        return services;
    }
}
