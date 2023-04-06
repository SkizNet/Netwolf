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
        services.AddSingleton<Client.INetworkFactory, Client.NetworkFactory>();
        services.AddSingleton<Client.ICommandFactory, Client.CommandFactory>();
        services.AddSingleton<Client.IConnectionFactory, Client.ConnectionFactory>();

        return services;
    }
}
