namespace Netwolf.Transport.IRC;

public enum AuthType
{
    None,
    NickServIdentify,
    NickServCertFp,
    SaslPlain,
    SaslExternal,
    SaslScramSha256
}
