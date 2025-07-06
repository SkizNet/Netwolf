using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Users;

public record ScramParameters(
    HashAlgorithmName Algorithm,
    ImmutableArray<byte> Salt,
    int IterationCount,
    ImmutableDictionary<char, string> ExtensionData)
{
    /// <summary>
    /// Declares that there is no extension data. This is used to save a bit of typing
    /// and be more semantically meaningful compared to ImmutableDictionary<char, string>.Empty.
    /// </summary>
    public static readonly ImmutableDictionary<char, string> NoExtensions = ImmutableDictionary<char, string>.Empty;

    /// <summary>
    /// Generates an extension data dictionary.
    /// </summary>
    /// <param name="extensions"></param>
    /// <returns></returns>
    public static ImmutableDictionary<char, string> MakeExtensionData(params IEnumerable<(char, string)> extensions)
    {
        var builder = ImmutableDictionary.CreateBuilder<char, string>();
        foreach (var (key, value) in extensions)
        {
            builder.Add(key, value);
        }

        return builder.ToImmutable();
    }
}
