using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Netwolf.PluginFramework.Commands;
using Netwolf.PluginFramework.Context;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.PluginFramework.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPluginFrameworkServices(this IServiceCollection services)
    {
        services.TryAddSingleton(typeof(ICommandDispatcher<>), typeof(CommandDispatcher<>));
        services.TryAddSingleton(typeof(ICommandValidator<>), typeof(CommandValidator<>));
        services.TryAddSingleton<IContextAugmenter, DummyContextAugmenter>();

        return services;
    }
}
