using Netwolf.Server.Commands;
using Netwolf.Server.Internal;
using Netwolf.Transport.Client;

using System.Collections.Concurrent;

namespace Netwolf.Server;

public class User
{
    public Network Network { get; init; }

    // temporary probably; we eventually want to support multiple clients attached to a single user,
    // and also potentially per-channel/group "profiles" for the user, which means most of these details won't be (only) top-level
    internal BlockingCollection<ICommand> Queue { get; init; } = new();

    public string Nickname { get; internal set; } = null!;

    public string Ident { get; internal set; } = null!;

    public string RealHost { get; internal set; } = null!;

    public string VirtualHost { get; internal set; } = null!;

    public string? Account { get; internal set; }

    public string RealName { get; internal set; } = null!;

    public string? FullIdent { get; internal set; }

    public string UserParam1 { get; internal set; } = null!;

    public string UserParam2 { get; internal set; } = null!;

    internal RegistrationFlags RegistrationFlags { get; private set; } = RegistrationFlags.Default;

    /// <summary>
    /// Whether the client has been fully registered on the network (has sent NICK/USER and finished CAP negotation)
    /// Has nothing to do with accounts
    /// </summary>
    public bool Registered => RegistrationFlags == RegistrationFlags.None;

    /// <summary>
    /// For display only
    /// </summary>
    public string ModeString => "+";

    public string Hostmask => $"{Nickname}!{Ident}@{VirtualHost}";

    // for channels and privs, probably want the public facing to be read-only/immutable
    // and only manipulated internally
    public List<Channel> Channels { get; init; } = new();

    public HashSet<string> UserPrivileges { get; init; } = new();

    public HashSet<string> OperPrivileges { get; init; } = new();

    public User(Network network)
    {
        Network = network;
    }

    public bool HasPrivilege(string priv, Channel? channel = null)
    {
        if (priv.Split(':', 2) is not [string scope, _])
        {
            throw new ArgumentException("Invalid privilege, expected scope:what", nameof(priv));
        }

        var container = scope switch
        {
            "user" => UserPrivileges,
            "chan" => channel?.GetPrivilegesFor(this) ?? throw new ArgumentNullException(nameof(channel)),
            "oper" => OperPrivileges,
            _ => throw new ArgumentException($"Unknown priv scope {scope}", nameof(priv))
        };

        if (container.Contains(priv))
        {
            return true;
        }

        string prefix = scope;
        foreach (string? piece in priv.Split(':').Skip(1))
        {
            if (container.Contains($"{prefix}:*"))
            {
                return true;
            }

            prefix = $"{prefix}:{piece}";
        }

        return false;
    }

    public bool HasAllPrivileges(Channel channel, params string[] privs)
    {
        return privs.All(p => HasPrivilege(p, channel));
    }

    public bool HasAllPrivileges(params string[] privs)
    {
        return privs.All(p => HasPrivilege(p));
    }

    public bool HasAnyPrivilege(Channel channel, params string[] privs)
    {
        return privs.Any(p => HasPrivilege(p, channel));
    }

    public bool HasAnyPrivilege(params string[] privs)
    {
        return privs.Any(p => HasPrivilege(p));
    }

    public void Send(string? source, string verb, IReadOnlyList<string?>? args = null, IReadOnlyDictionary<string, string?>? tags = null)
    {
        // TODO: inject tags based on negotated CAPs (or strip tags if client didn't do CAP negotation at all)
        var command = Network.CommandFactory.CreateCommand(
            CommandType.Server,
            source ?? Network.ServerName,
            verb,
            args ?? new List<string?>(),
            tags ?? new Dictionary<string, string?>());

        // TODO: Check for SendQ limits here
        Queue.Add(command);
    }

    internal MultiResponse? ClearRegistrationFlag(RegistrationFlags flag)
    {
        if (!RegistrationFlags.HasFlag(flag))
        {
            // already cleared, don't need to do anything
            return null;
        }

        RegistrationFlags ^= flag;

        if (Registered)
        {
            // connection has now been fully registered, send them all of the post-registration info
            var batch = new MultiResponse();
            batch.AddNumeric(this, Numeric.RPL_WELCOME);
            batch.AddNumeric(this, Numeric.RPL_YOURHOST, RealHost);
            batch.AddNumeric(this, Numeric.RPL_CREATED);
            batch.AddNumeric(this, Numeric.RPL_MYINFO, Network.ServerName, Network.Version, Network.UserModes, Network.ChannelModes, Network.ChannelModesWithParams);
            batch.AddRange(Network.ReportISupport(this));
            batch.AddRange(ListUsersCommand.ExecuteInternal(this));
            batch.AddNumeric(this, Numeric.RPL_UMODEIS, ModeString);
            batch.AddRange(MotdCommand.ExecuteInternal(this));

            return batch;
        }

        return null;
    }

    internal bool AttachConnectionConfig(string? password)
    {
        if (!RegistrationFlags.HasFlag(RegistrationFlags.PendingPass))
        {
            throw new InvalidOperationException($"A connection config has already been attached to this user.");
        }

        if (password != null)
        {
            // don't support server passwords right now
            return false;
        }

        // TODO: actually check configs; right now we just accept everyone that doesn't specify a server password
        RegistrationFlags |= RegistrationFlags.NeedsIdentLookup | RegistrationFlags.NeedsHostLookup;

        // mark connection as having a config attached (by clearing the fact we're looking for a PASS command)
        // this will never complete user registration so directly manipulate flags rather than calling ClearRegistrationFlag()
        RegistrationFlags ^= RegistrationFlags.PendingPass;

        return true;
    }

    /// <summary>
    /// If clearing <paramref name="flag"/> would cause registration to complete, attach
    /// a connection config as if no PASS command were supplied.
    /// </summary>
    /// <param name="flag">Flag that will be cleared</param>
    /// <returns>true if we attached a config or we don't need to attach a config yet, false if we need to attach a config but none matches (access denied)</returns>
    internal bool MaybeDoImplicitPassCommand(RegistrationFlags flag)
    {
        if ((RegistrationFlags & ~(flag | RegistrationFlags.PendingPass)) == RegistrationFlags.None)
        {
            return AttachConnectionConfig(null);
        }

        return true;
    }
}
