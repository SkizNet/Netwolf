using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Netwolf.Server.Internal;
using Netwolf.Transport.Extensions;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Joins;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Users;

internal class AccountProviderFactory : IAccountProviderFactory
{
    private IServiceProvider ServiceProvider { get; init; }

    private string? DefaultRealm { get; init; }

    private readonly List<(Glob Pattern, Type Provider)> _map = [];

    private readonly Dictionary<Type, IAccountProvider> _cache = [];

    public AccountProviderFactory(IServiceProvider serviceProvider, IOptionsSnapshot<ServerOptions> options)
    {
        ServiceProvider = serviceProvider;
        DefaultRealm = options.Value.DefaultRealm;

        // We use the first match, so we finagle the map to make matches that don't use wildcards first,
        // followed by those that only use ? wildcards, then those that use * wildcards. In each grouping,
        // we also go in order of longest to shortest.
        var sortedMap = options.Value.RealmMap
            .OrderBy(kvp => kvp.Key.AsSpan().Count('*'))
            .ThenBy(kvp => kvp.Key.AsSpan().Count('?'))
            .ThenByDescending(kvp => kvp.Key.Length)
            .ThenBy(kvp => kvp.Key);

        foreach (var (pattern, type) in sortedMap)
        {
            _map.Add((Glob.For(pattern), type));
        }
    }

    public IAccountProvider GetAccountProvider(ReadOnlySpan<byte> username)
    {
        // get realm from username if one exists
        int atSign = username.IndexOf((byte)'@');
        string realm;

        if (atSign == 0 || atSign == username.Length - 1)
        {
            // empty username or realm, which is an error
            return UnresolvedAccountProvider.Instance;
        }
        else if (atSign == -1)
        {
            // no realm, use default
            if (DefaultRealm == null)
            {
                return UnresolvedAccountProvider.Instance;
            }

            realm = DefaultRealm;
        }
        else
        {
            // extract realm from username
            try
            {
                realm = username[(atSign + 1)..].DecodeUtf8();
            }
            catch (ArgumentException)
            {
                // invalid UTF-8 sequence in realm
                return UnresolvedAccountProvider.Instance;
            }
        }

        foreach (var (pattern, provider) in _map)
        {
            if (pattern.IsMatch(realm))
            {
                if (!_cache.TryGetValue(provider, out var accountProvider))
                {
                    // create a new instance of the provider
                    accountProvider = (IAccountProvider)ActivatorUtilities.CreateInstance(ServiceProvider, provider);
                    _cache[provider] = accountProvider;
                }

                return accountProvider;
            }
        }

        // no matching providers found?
        return UnresolvedAccountProvider.Instance;
    }
}
