namespace Netwolf.Transport.Client
{
    public enum AuthType
    {
        None,
        NickServIdentify,
        NickServCertFp,
        SaslPlain,
        SaslExternal,
        SaslScramSha256
    }
}
