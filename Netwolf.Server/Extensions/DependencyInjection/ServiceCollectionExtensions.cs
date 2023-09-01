using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Netwolf.Server.Capabilities;
using Netwolf.Server.Commands;
using Netwolf.Server.ISupport;
using Netwolf.Server.Users;

using System.Runtime.CompilerServices;

namespace Netwolf.Server.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddServerServices(this IServiceCollection services)
    {
        AddServerServicesBase(services);
        services.AddOptions<ServerOptions>()
            .ValidateDataAnnotations();

        return services;
    }

    public static IServiceCollection AddServerServices(this IServiceCollection services, Action<ServerOptions> configureOptions)
    {
        AddServerServicesBase(services);
        services.AddOptions<ServerOptions>()
            .Configure(configureOptions)
            .ValidateDataAnnotations();

        return services;
    }

    private static IServiceCollection AddServerServicesBase(IServiceCollection services)
    {
        // TODO: get rid of the Network class entirely.
        // a service scope indicates a "network" and we should just expose the necessary lookups, registries, etc. from there
        services.AddScoped<Network>();
        services.AddScoped<ICommandDispatcher, CommandDispatcher>();
        services.AddScoped<IISupportResolver, ISupportResolver>();
        services.AddScoped<IUserFactory, UserFactory>();
        services.AddScoped<ICapabilityManager, CapabilityManager>();

        // let consumers register an IConfigureOptions<ServerOptions> if they wish, but these are here in case we need them later
        //services.AddTransient<IPostConfigureOptions<ServerOptions>, PostConfigureServerOptions>();
        //services.AddTransient<IValidateOptions<ServerOptions>, ValidateServerOptions>();

        return services;
    }
}
