using Microsoft.Extensions.DependencyInjection;

namespace Netwolf.Transport.Extensions.DependencyInjection;

/// <summary>
/// This class contains extension methods to register DI services
/// necessary for the Netwolf.Transport library.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTransportServices(this IServiceCollection services)
    {
        // Client services
        services.AddSingleton<IRC.INetworkFactory, IRC.NetworkFactory>();
        services.AddSingleton<IRC.ICommandFactory, IRC.CommandFactory>();
        services.AddSingleton<IRC.IConnectionFactory, IRC.ConnectionFactory>();

        return services;
    }
}
