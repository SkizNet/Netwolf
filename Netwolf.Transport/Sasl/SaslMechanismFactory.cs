// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.IRC;
using Netwolf.Transport.State;

namespace Netwolf.Transport.Sasl;

public class SaslMechanismFactory : ISaslMechanismFactory
{
    public ISaslMechanism CreateMechanism(string mechanism, NetworkOptions options)
    {
        return mechanism switch
        {
            "EXTERNAL" => new SaslExternal(options.ImpersonateAccount),
            "SCRAM-SHA-512-PLUS" => new SaslScram("SHA-512", options.AccountName, options.ImpersonateAccount, options.AccountPassword!, plus: true),
            "SCRAM-SHA-512" => new SaslScram("SHA-512", options.AccountName, options.ImpersonateAccount, options.AccountPassword!),
            "SCRAM-SHA-256-PLUS" => new SaslScram("SHA-256", options.AccountName, options.ImpersonateAccount, options.AccountPassword!, plus: true),
            "SCRAM-SHA-256" => new SaslScram("SHA-256", options.AccountName, options.ImpersonateAccount, options.AccountPassword!),
            "SCRAM-SHA-1-PLUS" => new SaslScram("SHA-1", options.AccountName, options.ImpersonateAccount, options.AccountPassword!, plus: true),
            "SCRAM-SHA-1" => new SaslScram("SHA-1", options.AccountName, options.ImpersonateAccount, options.AccountPassword!),
            "PLAIN" => new SaslPlain(options.AccountName, options.ImpersonateAccount, options.AccountPassword!),
            _ => throw new ArgumentException("Unsupported SASL mechanism", nameof(mechanism))
        };
    }

    public IEnumerable<string> GetSupportedMechanisms(NetworkOptions options, ServerRecord server)
    {
        List<string> supported = [];

        if (options.AccountCertificateFile != null && server.SecureConnection)
        {
            supported.Add("EXTERNAL");
        }

        if (options.AccountPassword != null)
        {
            // prefer channel binding variants over ones without,
            // with exception of SHA-1 which is deprioritized
            supported.Add("SCRAM-SHA-512-PLUS");
            supported.Add("SCRAM-SHA-256-PLUS");
            supported.Add("SCRAM-SHA-512");
            supported.Add("SCRAM-SHA-256");
            supported.Add("SCRAM-SHA-1-PLUS");
            supported.Add("SCRAM-SHA-1");

            if (server.SecureConnection || options.AllowInsecureSaslPlain)
            {
                supported.Add("PLAIN");
            }
        }

        return supported;
    }
}
