using Microsoft.Extensions.DependencyInjection;

using Netwolf.Server.Commands;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddServerServices(this IServiceCollection services)
        {
            services.AddSingleton<ICommandHandlerFactory, CommandHandlerFactory>();

            return services;
        }
    }
}
