using Microsoft.Extensions.DependencyInjection;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Transport.Extensions.DependencyInjection
{
    /// <summary>
    /// This class contains extension methods to register DI services
    /// necessary for the Netwolf.Transport library.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        [SuppressMessage("Style", "IDE0001:Simplify name",
            Justification = "Explicitly listing generic types in registration makes it more understandable which types are being registered")]
        public static IServiceCollection AddTransportServices(this IServiceCollection services)
        {
            // Client services
            services.AddSingleton<Client.INetworkFactory, Client.NetworkFactory>();
            services.AddScoped<Client.INetwork>(provider => provider.GetRequiredService<Client.INetworkFactory>().GetFromScope(provider));

            return services;
        }
    }
}
