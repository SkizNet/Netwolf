using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Netwolf.Server.Internal;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Capabilities;

public class CapabilityManager : ICapabilityManager
{
    private Dictionary<string, ICapability> Capabilities { get; init; } = new();

    public CapabilityManager(IServiceProvider provider, ILogger<ICapabilityManager> logger, IOptionsSnapshot<ServerOptions> options)
    {
        logger.LogTrace("Scanning for capabilities");
        foreach (var cap in TypeDiscovery.GetTypes<ICapability>(provider, options))
        {
            if (Capabilities.TryGetValue(cap.Name, out var value))
            {
                logger.LogWarning("{Type1} introduces duplicate capability {Name}; already defined by {Type2}",
                    cap.GetType().FullName,
                    cap.Name,
                    value.GetType().FullName);

                continue;
            }

            logger.LogTrace("Found {Type} providing {Name}", cap.GetType().FullName, cap.Name);
            Capabilities[cap.Name] = cap;
        }
    }

    public IEnumerable<ICapability> GetAllCapabilities()
    {
        return Capabilities.Values;
    }

    public bool ApplyCapabilitySet(User client, IEnumerable<string> add, IEnumerable<string> remove)
    {
        // calculate the new capability set after changes
        HashSet<ICapability> newSet = new(client.Capabilities);
        try
        {
            newSet.UnionWith(add.Select(x => Capabilities[x]));
            newSet.ExceptWith(remove.Select(x => Capabilities[x]));
        }
        catch (KeyNotFoundException)
        {
            // passed a capability name we don't recognize; reject the change
            return false;
        }

        // ensure all dependencies are met
        foreach (var cap in newSet)
        {
            foreach (var dep in cap.DependsOn)
            {
                if (!newSet.Contains(dep))
                {
                    return false;
                }
            }

            foreach (var conflict in cap.ConflictsWith)
            {
                if (newSet.Contains(conflict))
                {
                    return false;
                }
            }
        }

        client.Capabilities = newSet;
        return true;
    }
}
