namespace Netwolf.Transport.IRC;

/// <summary>
/// Data class to encapsulate options for an <see cref="INetwork"/>
/// </summary>
public class NetworkOptions
{
    private string? _ident;
    private string? _realName;
    private string? _accountName;

    /// <summary>
    /// How long to wait before abandoning a connection to a particular <see cref="Server"/>.
    /// </summary>
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Number of times we will attempt to reconnect to a network upon getting a non-fatal connection error.
    /// A single retry involves going through every defined Server for the network. A value of 0 for this
    /// option indicates we will only go through all of the servers once.
    /// </summary>
    public int ConnectRetries { get; set; } = Int32.MaxValue;

    /// <summary>
    /// How often to ping the remote <see cref="Server"/> to see if the connection is still live.
    /// </summary>
    public TimeSpan PingInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long to wait for a ping reply before we consider the connection dead?
    /// </summary>
    public TimeSpan PingTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Primary nickname we attempt to use when connecting to this network
    /// </summary>
    /// <remarks>
    /// <seealso cref="SecondaryNick"/>
    /// </remarks>
    public string PrimaryNick { get; set; } = String.Empty;

    /// <summary>
    /// Secondary nickname we attempt to use when connecting to this network,
    /// in the event <see cref="PrimaryNick"/> is in use.
    /// </summary>
    public string? SecondaryNick { get; set; }

    /// <summary>
    /// Ident to use. Defaults to <see cref="PrimaryNick"/> if not explicitly set.
    /// </summary>
    public string Ident
    {
        get => _ident ?? PrimaryNick;
        set => _ident = value;
    }

    /// <summary>
    /// Real name to use. Defaults to <see cref="PrimaryNick"/> if not explicitly set.
    /// </summary>
    public string RealName
    {
        get => _realName ?? PrimaryNick;
        set => _realName = value;
    }

    /// <summary>
    /// List of servers to try when connecting (in order of preference).
    /// </summary>
    public List<IServer> Servers { get; init; } = new List<IServer>();

    /// <summary>
    /// Password required to connect to the network, if any.
    /// </summary>
    public string? ServerPassword { get; set; }

    /// <summary>
    /// Local host to bind to when connecting. If unset, use default outbound IP.
    /// </summary>
    public string? BindHost { get; set; }

    /// <summary>
    /// If true, disables certificate validation. This reduces security and should be used carefully. If true,
    /// the <see cref="TrustedFingerprints"/> and <see cref="CheckOnlineRevocation"/> settings have no effect.
    /// </summary>
    public bool AcceptAllCertificates { get; set; }

    /// <summary>
    /// A list of trusted certificate fingerprints. If this is defined, CA validation will not be performed,
    /// and the server's certificate must be hash to one of the fingerprints listed here. The format of entries
    /// should be hexidecimal SHA-256 fingerprints of certificates, with or without <c>:</c> divider characters.
    /// The strings are compared case-insensitively. If this list is not empty,
    /// <see cref="CheckOnlineRevocation"/> will have no effect.
    /// </summary>
    public List<string> TrustedFingerprints { get; init; } = new List<string>();

    /// <summary>
    /// If true, checks the certificate's OCSP responder for revocation information before accepting a connection.
    /// If the responder indicates that the certificate is revoked or the responder times out, the connection
    /// will be aborted.
    /// </summary>
    public bool CheckOnlineRevocation { get; set; }

    /// <summary>
    /// Account name to use. Defaults to <see cref="PrimaryNick"/> if not explicitly set.
    /// </summary>
    public string AccountName
    {
        get => _accountName ?? PrimaryNick;
        set => _accountName = value;
    }

    /// <summary>
    /// Should we attempt to impersonate another account? Some services may allow this with sufficient privileges.
    /// This should be the account name of the account to impersonate, or <c>null</c> if not impersonating.
    /// Has no effect if SASL is not in use.
    /// </summary>
    public string? ImpersonateAccount { get; set; }

    /// <summary>
    /// Password to log into the user's account via SASL.
    /// </summary>
    public string? AccountPassword { get; set; }

    /// <summary>
    /// File path of TLS client certificate used to authenticate to the user's account.
    /// </summary>
    public string? AccountCertificateFile { get; set; }

    /// <summary>
    /// Password for the TLS client certificate, if any.
    /// </summary>
    public string? AccountCertificatePassword { get; set; }

    /// <summary>
    /// Authentication type to use. If <c>true</c> (default), attempts SASL authentication
    /// based on the network's SASL support and whether <see cref="AccountCertificate"/> or
    /// <see cref="AccountPassword"/> are defined. By default, EXTERNAL, SCRAM-*, and PLAIN
    /// are supported, although consumers may replace the <see cref="Sasl.ISaslMechanismFactory"/>
    /// service to support additional algorithms.
    /// </summary>
    public bool UseSasl { get; set; } = true;

    /// <summary>
    /// If <c>true</c>, allow SASL PLAIN even over connections that are not encrypted.
    /// This is generally insecure, although if encryption is performed at a lower level (e.g. VPN/tor)
    /// then it may be fine to use.
    /// </summary>
    public bool AllowInsecureSaslPlain { get; set; } = false;

    /// <summary>
    /// If we attempt SASL and it fails, should we continue with the connection?
    /// This has no effect if the server doesn't advertise SASL support.
    /// </summary>
    public bool AbortOnSaslFailure { get; set; } = true;

    /// <summary>
    /// SASL mechanisms we will never attempt, even if supported by the server and our config.
    /// Values must be ALL UPPERCASE.
    /// </summary>
    public HashSet<string> DisabledSaslMechs { get; init; } = new();

    /// <summary>
    /// If set, and supported by the server, use CPRIVMSG and CNOTICE for outgoing messages
    /// when possible (i.e. when opped in a channel shared with the message targets).
    /// CPRIVMSG and CNOTICE bypass certain flood controls to make sending possibly a bit faster
    /// on the IRC network side, but can be disabled in case of incompatibilities client-side
    /// or a broken implementation network-side.
    /// </summary>
    public bool UseCPrivmsg { get; set; }
}
