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
    string Salt,
    int IterationCount,
    ImmutableDictionary<char, string> ExtensionData);
