using Netwolf.Transport.IRC;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.ISupport;

public interface IISupportTokenProvider
{
    /// <summary>
    /// All ISUPPORT tokens this provider is capable of providing, used for startup logging.
    /// A call to <see cref="GetTokens(User)"/> will not necessarily populate all of the tokens
    /// listed here, as some tokens may be dependent on e.g. the connection class of the client
    /// or network configuration.
    /// </summary>
    IEnumerable<ISupportToken> ProvidedTokens { get; }

    /// <summary>
    /// Retrieve all ISUPPORT tokens valid for the given client.
    /// </summary>
    /// <param name="client"></param>
    /// <returns></returns>
    IReadOnlyDictionary<ISupportToken, object?> GetTokens(User client);

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
    object? MergeTokens(ISupportToken key, IEnumerable<object> tokens) => null;
}
