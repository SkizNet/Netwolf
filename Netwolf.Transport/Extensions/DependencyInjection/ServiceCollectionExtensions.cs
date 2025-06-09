// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Netwolf.Transport.IRC;
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
        services.TryAddSingleton<INetworkFactory, NetworkFactory>();
        services.TryAddSingleton<IConnectionFactory, ConnectionFactory>();
        services.TryAddSingleton<ISaslMechanismFactory, SaslMechanismFactory>();

        // General services (used for both client and server)
        services.TryAddSingleton<ICommandFactory, CommandFactory>();
        services.TryAddSingleton(typeof(ICommandValidator<>), typeof(CommandValidator<>));
        services.TryAddScoped<IValidationContextFactory, ValidationContextFactory>();

        return services;
    }
}
