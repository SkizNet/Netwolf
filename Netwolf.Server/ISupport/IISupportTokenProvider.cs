using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.ISupport;

public interface IISupportTokenProvider
{
    IReadOnlyDictionary<string, object?> GetTokens(Network network, User client);

    /// <summary>
    /// Merge multiple values together into a single ISUPPORT token.
    /// Implementations must return <c>null</c> for keys they do not recognize.
    /// This is only called if 2+ token providers return the same token.
    /// If your token does not support merging, do not implement this method;
    /// the runtime will throw an exception if 2+ token providers return the same token in that case.
    /// </summary>
    /// <param name="key">The ISUPPORT parameter being merged</param>
    /// <param name="tokens">The tokens to merge together into the parameter. All items are guaranteed to be non-null.</param>
    /// <returns>The merged token, or null if this token provider is not responsible for the specified key</returns>
    object? MergeTokens(string key, IEnumerable<object> tokens) => null;
}
