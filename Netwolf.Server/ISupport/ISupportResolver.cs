using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Netwolf.Server.Commands;
using Netwolf.Server.Internal;

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

    public ISupportResolver(IServiceProvider serviceProvider, ILogger<IISupportResolver> logger, IOptionsSnapshot<ServerOptions> options)
    {
        Logger = logger;

        // DefaultTokenProvider should always be first; it's an internal class so the loop below won't construct a 2nd one
        TokenProviders = [(IISupportTokenProvider)ActivatorUtilities.CreateInstance(serviceProvider, typeof(DefaultTokenProvider))];

        // populate TokenProviders from all concrete classes across all assemblies that implement IISupportTokenProvider
        Logger.LogTrace("Scanning for ISUPPORT token providers");
        foreach (var provider in TypeDiscovery.GetTypes<IISupportTokenProvider>(serviceProvider, options))
        {
            TokenProviders.Add(provider);
            logger.LogTrace("Found {Type}", provider.GetType().FullName);
        }
    }

    public IReadOnlyDictionary<string, object?> Resolve(User user)
    {
        Dictionary<string, object?> tokens = [];
        Dictionary<string, List<object?>> staging = [];

        foreach (var provider in TokenProviders)
        {
            var res = provider.GetTokens(user);
            foreach (var (key, token) in res)
            {
                if (staging.TryGetValue(key, out var value))
                {
                    value.Add(token);
                }
                else
                {
                    staging[key] = [token];
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
