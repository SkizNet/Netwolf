using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Netwolf.Server.Commands;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.ISupport;

public class ISupportResolver : IISupportResolver
{
    private ILogger<IISupportResolver> Logger { get; init; }

    private List<IISupportTokenProvider> TokenProviders { get; init; }

    public ISupportResolver(IServiceProvider serviceProvider, ILogger<IISupportResolver> logger)
    {
        Logger = logger;

        // DefaultTokenProvider should always be first; it's an internal class so the loop below won't construct a 2nd one
        TokenProviders = new() { new DefaultTokenProvider() };

        // populate TokenProviders from all concrete classes across all assemblies that implement IISupportTokenProvider
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Logger.LogTrace("Scanning {Assembly} for ISUPPORT token providers", assembly.FullName);

            foreach (var type in assembly.ExportedTypes)
            {
                if (type.IsAbstract || !type.IsAssignableTo(typeof(IISupportTokenProvider)))
                {
                    continue;
                }

                TokenProviders.Add((IISupportTokenProvider)ActivatorUtilities.CreateInstance(serviceProvider, type));
                logger.LogTrace("Found {Type}", type.FullName);
            }
        }
    }

    public IReadOnlyDictionary<string, object?> Resolve(Network network, User user)
    {
        Dictionary<string, object?> tokens = new();
        Dictionary<string, List<object?>> staging = new();

        foreach (var provider in TokenProviders)
        {
            var res = provider.GetTokens(network, user);
            foreach (var (key, token) in res)
            {
                if (staging.ContainsKey(key))
                {
                    staging[key].Add(token);
                }
                else
                {
                    staging[key] = new List<object?>() { token };
                }
            }
        }

        foreach (var key in staging.Keys)
        {
            // not a foreach variable because we might mutate this
            var pieces = staging[key];

            // When mixing paramless and parameters, only consider parameters
            if (pieces.Any(s => s != null))
            {
                pieces = pieces.Where(s => s != null).ToList();
            }

            // Only one value? Use it
            if (pieces.Count == 1)
            {
                tokens[key] = pieces[0];
                continue;
            }

            // Multiple places tried to define this as a parameterless token, so do that
            if (pieces.All(s => s == null))
            {
                tokens[key] = null;
                continue;
            }

            // We have multiple parameter values, try to merge them
            // all elements of pieces are guaranteed to be non-null at this point
            foreach (var provider in TokenProviders)
            {
                var merged = provider.MergeTokens(key, pieces!);
                if (merged != null)
                {
                    tokens[key] = merged;
                    goto next;
                }
            }

            // merge failed, use the first-defined piece
            Logger.LogWarning("Received multiple ISUPPORT token values for non-mergable key {Key}", key);
            tokens[key] = pieces[0];

        next:;
        }

        return tokens;
    }
}
