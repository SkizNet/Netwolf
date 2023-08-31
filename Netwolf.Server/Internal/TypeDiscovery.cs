using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Netwolf.Server.Capabilities;
using Netwolf.Server.ChannelModes;
using Netwolf.Server.Commands;
using Netwolf.Server.ISupport;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Internal;

internal static class TypeDiscovery
{
    internal static IEnumerable<T> GetTypes<T>(IServiceProvider provider, IOptionsSnapshot<ServerOptions> options)
    {
        List<Type> collection;

        if (typeof(T) == typeof(ICommandHandler))
        {
            collection = options.Value.EnabledCommands;
        }
        else if (typeof(T) == typeof(ICapability))
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
            collection = new();
            collection.AddRange(options.Value.EnabledCommands);
            collection.AddRange(options.Value.EnabledCapabilities);
            collection.AddRange(options.Value.EnabledChannelModes);
        }
        else
        {
            throw new ArgumentException("The generic type parameter provided does not support type discovery");
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var type in assembly.ExportedTypes)
            {
                if (type.IsAbstract || !type.IsAssignableTo(typeof(T)) || !collection.Contains(typeof(T)))
                {
                    continue;
                }

                yield return (T)ActivatorUtilities.CreateInstance(provider, type);
            }
        }
    }
}
