using Microsoft.Extensions.DependencyInjection;

using Netwolf.Server.Commands;
using Netwolf.Server.ISupport;
using Netwolf.Server.Users;

namespace Netwolf.Server.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddServerServices(this IServiceCollection services)
    {
        services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
        services.AddSingleton<IISupportResolver, ISupportResolver>();
        // TODO: get rid of the Network class entirely.
        // a service scope indicates a "network" and we should just expose the necessary lookups, registries, etc. from there
        services.AddScoped<Network>();
        services.AddScoped<IUserFactory, UserFactory>();

        return services;
    }
}
