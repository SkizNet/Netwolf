﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Netwolf.Server.Capabilities;
using Netwolf.Server.ChannelModes;
using Netwolf.Server.ISupport;
using Netwolf.Server.Sasl;

namespace Netwolf.Server.Internal;

internal static class TypeDiscovery
{
    internal static IEnumerable<T> GetTypes<T>(IServiceProvider provider, ILogger logger, IOptionsSnapshot<ServerOptions> options)
    {
        List<Type> collection;

        if (typeof(T) == typeof(ICapability))
        {
            collection = options.Value.EnabledCapabilities;
        }
        else if (typeof(T) == typeof(IChannelMode))
        {
            collection = options.Value.EnabledChannelModes;
        }
        else if (typeof(T) == typeof(IISupportTokenProvider))
        {
            // ISUPPORT token providers are only enabled if they were also enabled via some other mechanism
            collection =
            [
                .. options.Value.EnabledCommands,
                .. options.Value.EnabledCapabilities,
                .. options.Value.EnabledChannelModes,
            ];
        }
        else if (typeof(T) == typeof(ISaslMechanismProvider))
        {
            collection = options.Value.EnabledSaslMechanisms;
        }
        else
        {
            throw new ArgumentException("The generic type parameter provided does not support type discovery");
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            // skip over assemblies that we know won't contain things we care about
            if (assembly.FullName?.StartsWith("System.") == true || assembly.FullName?.StartsWith("Microsoft.") == true)
            {
                continue;
            }

            logger.LogTrace("Examining {Assembly}", assembly.FullName);

            foreach (var type in assembly.ExportedTypes)
            {
                if (type.IsAbstract || !type.IsAssignableTo(typeof(T)) || !collection.Contains(type))
                {
                    continue;
                }

                yield return (T)ActivatorUtilities.CreateInstance(provider, type);
            }
        }
    }
}
