using Netwolf.Server.Commands;
using Netwolf.Server.Extensions.Internal;
using Netwolf.Server.Internal;
using Netwolf.Transport.IRC;
using Netwolf.Transport.Extensions;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Netwolf.Server.Capabilities;
using Netwolf.PluginFramework.Commands;
using Netwolf.PluginFramework.Context;
using Microsoft.Extensions.Options;
using Netwolf.Transport.Commands;
using Netwolf.Server.Sasl;

namespace Netwolf.Server;

public class User : IDisposable
{
    private bool disposedValue;

    private ICommandFactory CommandFactory { get; init; }

    private ServerOptions Options { get; init; }

    public Network Network { get; init; }

    // temporary probably; we eventually want to support multiple clients attached to a single user,
    // and also potentially per-channel/group "profiles" for the user, which means most of these details won't be (only) top-level
    internal BlockingCollection<ICommand> Queue { get; init; } = new();

    public int MaxBytesPerLine { get; set; } = 512;

    public string Nickname { get; internal set; } = null!;

    public string LookupKey => IrcUtil.Casefold(Nickname, CaseMapping.Ascii);

    public string Ident { get; internal set; } = null!;

    public IPAddress RealIP { get; private init; }

    public string RealHost { get; internal set; } = null!;

    public string? VirtualHost { get; internal set; }

    public string DisplayHost => VirtualHost ?? RealHost;

    public string? Account { get; internal set; }

    public string RealName { get; internal set; } = null!;

    public string? FullIdent { get; internal set; }

    internal string IdentPrefix { get; private set; } = String.Empty;

    public string UserParam1 { get; internal set; } = null!;

    public string UserParam2 { get; internal set; } = null!;

    internal ISaslState? SaslState { get; set; }

    internal int SaslFailures { get; set; } = 0;

    /// <summary>
    /// Highest client-specified version passed to CAP LS, or 301 if they didn't pass a version.
    /// 0 if the client never executed CAP LS or CAP REQ.
    /// </summary>
    public int CapabilityVersion { get; internal set; }

    internal HashSet<ICapability> Capabilities { get; set; } = [];

    private int LocalPort { get; init; }

    private int RemotePort { get; init; }

    // Probably temporary until we get a real user modes implementation
    public bool Invisible { get; internal set; } = false;

    // Also probably temporary, or at least needs a refactor for when user persistence becomes a thing
    public bool Away => AwayReason != null;

    public string? AwayReason { get; internal set; }

    private int _registrationFlags = (int)RegistrationFlags.Default;
    internal RegistrationFlags RegistrationFlags => (RegistrationFlags)_registrationFlags;

    private CancellationTokenSource TokenSource { get; init; } = new();

    private List<Task> Tasks { get; init; } = [];

    /// <summary>
    /// Whether the client has been fully registered on the network (has sent NICK/USER and finished CAP negotation)
    /// Has nothing to do with accounts
    /// </summary>
    public bool Registered => RegistrationFlags == RegistrationFlags.None;

    /// <summary>
    /// For display only
    /// </summary>
    public string ModeString => Invisible ? "+i" : "+";

    public string Hostmask => $"{Nickname}!{Ident}@{VirtualHost}";

    // for channels and privs, probably want the public facing to be read-only/immutable
    // and only manipulated internally
    public HashSet<Channel> Channels { get; init; } = [];

    public HashSet<string> UserPrivileges { get; init; } = [];

    public HashSet<string> OperPrivileges { get; init; } = [];

    public User(
        Network network,
        ICommandFactory commandFactory,
        IOptionsSnapshot<ServerOptions> options,
        IPAddress ip,
        int localPort,
        int remotePort)
    {
        Network = network;
        CommandFactory = commandFactory;
        Options = options.Value;
        RealIP = ip;
        LocalPort = localPort;
        RemotePort = remotePort;
        Tasks.Add(Task.Run(LookUpIdent));
        Tasks.Add(Task.Run(LookUpHost));
        Interlocked.Increment(ref Network._pendingCount);
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

    public bool HasCapability<T>()
        where T : ICapability
    {
        return Capabilities.Any(cap => cap.GetType() == typeof(T));
    }

    public void Send(string? source, string verb, IReadOnlyList<string?>? args = null, IReadOnlyDictionary<string, string?>? tags = null)
    {
        // TODO: inject tags based on negotated CAPs (or strip tags if client didn't do CAP negotation at all)
        var command = CommandFactory.CreateCommand(
            CommandType.Server,
            source ?? Network.ServerName,
            verb,
            args ?? new List<string?>(),
            tags ?? new Dictionary<string, string?>());

        // TODO: Check for SendQ limits here
        Queue.Add(command);
    }

    internal void AddRegistrationFlag(RegistrationFlags flag)
    {
        if (!Registered)
        {
            Interlocked.Or(ref _registrationFlags, (int)flag);
        }
    }

    internal ICommandResponse ClearRegistrationFlag(RegistrationFlags flag)
    {
        Interlocked.And(ref _registrationFlags, ~(int)flag);

        if (Registered)
        {
            // connection has now been fully registered
            Interlocked.Decrement(ref Network._pendingCount);
            Network.Clients[LookupKey] = this;
            var newClientCount = Network.Clients.Count;
            var currentMax = Network.MaxUserCount;
            if (newClientCount > currentMax)
            {
                Interlocked.CompareExchange(ref Network._maxUserCount, currentMax, newClientCount);
            }

            // send them all of the post-registration info
            var batch = new MultiResponse();
            batch.AddNumeric(this, Numeric.RPL_WELCOME);
            batch.AddNumeric(this, Numeric.RPL_YOURHOST);
            batch.AddNumeric(this, Numeric.RPL_CREATED);
            batch.AddNumeric(this, Numeric.RPL_MYINFO, Network.ServerName, Network.Version, Network.UserModes, Network.ChannelModes, Network.ChannelModesWithParams);
            batch.AddRange(Network.ReportISupport(this));
            batch.AddRange(ListUsersCommand.ExecuteInternal(this));
            if (ModeString != "+")
            {
                batch.AddNumeric(this, Numeric.RPL_UMODEIS, ModeString);
            }
            batch.AddRange(MotdCommand.ExecuteInternal(this));

            return batch;
        }

        return new EmptyResponse();
    }

    internal Task<bool> AttachConnectionConfig(string? password)
    {
        if (!RegistrationFlags.HasFlag(RegistrationFlags.PendingPass))
        {
            throw new InvalidOperationException($"A connection config has already been attached to this user.");
        }

        if (password != null)
        {
            // don't support server passwords right now
            return Task.FromResult(false);
        }

        // mark connection as having a config attached (by clearing the fact we're looking for a PASS command)
        ClearRegistrationFlag(RegistrationFlags.PendingPass);
        return Task.FromResult(true);
    }

    /// <summary>
    /// If clearing <paramref name="flag"/> would cause registration to complete, attach
    /// a connection config as if no PASS command were supplied.
    /// </summary>
    /// <param name="flag">Flag that will be cleared</param>
    /// <returns>true if we attached a config or we don't need to attach a config yet, false if we need to attach a config but none matches (access denied)</returns>
    internal async Task<bool> MaybeDoImplicitPassCommand(RegistrationFlags flag)
    {
        if ((RegistrationFlags & ~(flag | RegistrationFlags.PendingPass)) == RegistrationFlags.None)
        {
            return await AttachConnectionConfig(null);
        }

        return true;
    }

    private async Task LookUpIdent()
    {
        var tags = new Dictionary<string, string?>();
        string? ident = Ident;

        TokenSource.Token.ThrowIfCancellationRequested();
        if (LocalPort == 0 || RemotePort == 0 || !Options.EnabledFeatures.Contains(FeatureFlags.IdentLookups))
        {
            // skip lookup if we don't have port data
            Queue.Add(CommandFactory.CreateCommand(CommandType.Server, "irc.netwolf.org", "NOTICE", ["*", "*** Ident lookup disabled; not checking ident"], tags));

            if (!await MaybeDoImplicitPassCommand(RegistrationFlags.PendingIdentLookup))
            {
                // TODO: move string to a resource file for l10n
                new ErrorResponse(this, "You do not have access to this network (missing password?).").Send();
            }

            ClearRegistrationFlag(RegistrationFlags.PendingIdentLookup).Send();
            return;
        }

        // set up the ~ prefix (cleared if we get a valid ident response back)
        IdentPrefix = "~";
        Queue.Add(CommandFactory.CreateCommand(CommandType.Server, "irc.netwolf.org", "NOTICE", ["*", "*** Checking ident..."], tags));
        using var socket = new Socket(RealIP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        using var timeSource = new CancellationTokenSource();
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(TokenSource.Token, timeSource.Token);

        // give 5 seconds to complete the entire identd process
        timeSource.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            await socket.ConnectAsync(RealIP, 113, linkedSource.Token);
            await socket.SendAsync(Encoding.ASCII.GetBytes($"{RemotePort} , {LocalPort}\r\n"), linkedSource.Token);
            var buffer = new byte[1024];
            _ = await socket.ReceiveAsync(buffer, linkedSource.Token);
            socket.Shutdown(SocketShutdown.Both);

            // throws ArgumentException on invalid UTF-8
            var line = buffer.DecodeUtf8();
            var m = Regex.Match(line, $@"^\s*{RemotePort}\s*,\s*{LocalPort}\s*:\s*USERID\s*:[^:]+:(.+)\r\n$");

            if (!m.Success)
            {
                throw new ArgumentException("Ident response invalid or error");
            }

            ident = new string(m.Groups[1].Value
                // strip leading whitespace/nonprintables, as well as leading ^~
                .SkipWhile(c => (short)c <= 32 || c == '^' || c == '~')
                // read until we find a whitespace/nonprintable or @:
                .TakeWhile(c => (short)c > 32 && c != '@' && c != ':')
                .ToArray());

            if (ident == String.Empty)
            {
                throw new ArgumentException("Ident response invalid");
            }

            // valid ident, keep the full (untruncated) version in the record as well for matching purposes
            IdentPrefix = String.Empty;
            FullIdent = ident;
            Queue.Add(CommandFactory.CreateCommand(CommandType.Server, "irc.netwolf.org", "NOTICE", ["*", "*** Got ident response"], tags));
        }
        catch (Exception ex) when (ex is OperationCanceledException || ex is SocketException)
        {
            // if we were instructed to abort, do so early
            TokenSource.Token.ThrowIfCancellationRequested();
            Queue.Add(CommandFactory.CreateCommand(CommandType.Server, "irc.netwolf.org", "NOTICE", ["*", "*** No ident response; username prefixed with ~"], tags));
        }
        catch (ArgumentException)
        {
            Queue.Add(CommandFactory.CreateCommand(CommandType.Server, "irc.netwolf.org", "NOTICE", ["*", "*** Invalid ident response; username prefixed with ~"], tags));
        }
        finally
        {
            socket.Close();
        }

        if (ident != null)
        {
            Ident = $"{IdentPrefix}{ident}".Truncate(11);
        }

        if (!await MaybeDoImplicitPassCommand(RegistrationFlags.PendingIdentLookup))
        {
            // TODO: move string to a resource file for l10n
            new ErrorResponse(this, "You do not have access to this network (missing password?).").Send();
        }

        ClearRegistrationFlag(RegistrationFlags.PendingIdentLookup).Send();
    }

    private async Task LookUpHost()
    {
        var tags = new Dictionary<string, string?>();

        // TODO: refactor this and LookUpIdent to eliminate some code duplication for the "disabled" case
        if (!Options.EnabledFeatures.Contains(FeatureFlags.HostLookups))
        {
            Queue.Add(CommandFactory.CreateCommand(CommandType.Server, "irc.netwolf.org", "NOTICE", ["*", "*** Host lookup disabled; using your IP address"], tags));
            RealHost = RealIP.ToString();

            if (!await MaybeDoImplicitPassCommand(RegistrationFlags.PendingHostLookup))
            {
                // TODO: move string to a resource file for l10n
                new ErrorResponse(this, "You do not have access to this network (missing password?).").Send();
            }

            ClearRegistrationFlag(RegistrationFlags.PendingHostLookup).Send();
            return;
        }

        TokenSource.Token.ThrowIfCancellationRequested();
        Queue.Add(CommandFactory.CreateCommand(CommandType.Server, "irc.netwolf.org", "NOTICE", ["*", "*** Looking up your hostname..."], tags));

        using var timeSource = new CancellationTokenSource();
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(TokenSource.Token, timeSource.Token);
        bool resolved = false;
        timeSource.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            var entry = await Dns.GetHostEntryAsync(RealIP.ToString(), linkedSource.Token);
            if (entry.HostName != RealIP.ToString() && entry.AddressList.Contains(RealIP))
            {
                resolved = true;

                if (entry.HostName.Length > 63)
                {
                    RealHost = RealIP.ToString();
                    Queue.Add(CommandFactory.CreateCommand(CommandType.Server, "irc.netwolf.org", "NOTICE", ["*", "*** Hostname too long; using your IP address"], tags));
                }
                else
                {
                    RealHost = entry.HostName;
                    Queue.Add(CommandFactory.CreateCommand(CommandType.Server, "irc.netwolf.org", "NOTICE", ["*", "*** Found your hostname"], tags));
                }
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException || ex is SocketException)
        {
            // if we were instructed to abort, do so early
            TokenSource.Token.ThrowIfCancellationRequested();
        }

        if (!resolved)
        {
            RealHost = RealIP.ToString();
            Queue.Add(CommandFactory.CreateCommand(CommandType.Server, "irc.netwolf.org", "NOTICE", ["*", "*** Unable to resolve your hostname; using your IP address"], tags));
        }

        if (!await MaybeDoImplicitPassCommand(RegistrationFlags.PendingHostLookup))
        {
            // TODO: move string to a resource file for l10n
            new ErrorResponse(this, "You do not have access to this network (missing password?).").Send();
        }

        ClearRegistrationFlag(RegistrationFlags.PendingHostLookup).Send();
    }

    /// <summary>
    /// Mark client as disconnecting, cancelling any pending tasks for them
    /// </summary>
    internal void Disconnect()
    {
        TokenSource.Cancel();
        Task.WaitAll([.. Tasks]);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                Queue.Dispose();
                TokenSource.Dispose();
            }

            disposedValue = true;
        }
    }

    /// <summary>
    /// Dispose of this class instance. Ensure there are no pending tasks/threads that will be accessing it first!
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
