using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Netwolf.Transport.Sasl;

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
        services.TryAddSingleton<IRC.INetworkFactory, IRC.NetworkFactory>();
        services.TryAddSingleton<IRC.ICommandFactory, IRC.CommandFactory>();
        services.TryAddSingleton<IRC.IConnectionFactory, IRC.ConnectionFactory>();
        services.TryAddSingleton<ISaslMechanismFactory, SaslMechanismFactory>();

        return services;
    }
}
