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
        _ = services.AddSingleton<Client.INetworkFactory, Client.NetworkFactory>();
        _ = services.AddSingleton<Client.ICommandFactory, Client.CommandFactory>();
        _ = services.AddSingleton<Client.IConnectionFactory, Client.ConnectionFactory>();

        return services;
    }
}
