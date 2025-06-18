using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Netwolf.PluginFramework.Extensions.DependencyInjection;
using Netwolf.PluginFramework.Permissions;
using Netwolf.Transport.Commands;
using Netwolf.Transport.Context;
using Netwolf.Transport.Extensions.DependencyInjection;

namespace Netwolf.Test.PluginFramework;

public abstract class PluginFrameworkTestBase
{
    protected static IServiceScope CreateScope(Type? validatorType = null, IEnumerable<Type>? permissionTypes = null, IEnumerable<Type>? augmenterTypes = null)
    {
        var collection = new ServiceCollection()
            .AddLogging(config => config.SetMinimumLevel(LogLevel.Debug).AddConsole())
            .AddTransportServices()
            .AddPluginFrameworkServices();

        if (validatorType != null)
        {
            collection.AddSingleton(typeof(ICommandValidator<>), validatorType);
        }

        foreach (var type in permissionTypes ?? [])
        {
            collection.AddSingleton(typeof(IPermissionManager), type);
        }

        foreach (var type in augmenterTypes ?? [])
        {
            collection.AddSingleton(typeof(IContextAugmenter), type);
        }

        return collection.BuildServiceProvider().CreateScope();
    }
}
