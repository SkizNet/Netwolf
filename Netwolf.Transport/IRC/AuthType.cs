// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.Transport.IRC;

public enum AuthType
{
    /// <summary>
    /// Do not attempt to authenticate against an account
    /// </summary>
    None,
    /// <summary>
    /// Attempt to send an identify command to services once connected
    /// </summary>
    NickServIdentify,
    /// <summary>
    /// Rely on services automatically detecting our certificate fingerprint
    /// </summary>
    NickServCertFp,
    /// <summary>
    /// Use SASL with a password in plaintext
    /// </summary>
    SaslPlain,
    /// <summary>
    /// Use SASL with a client certificate
    /// </summary>
    SaslExternal,
    /// <summary>
    /// Use SASL with a password via a challenge-response mechanism
    /// </summary>
    SaslScramSha256
}
