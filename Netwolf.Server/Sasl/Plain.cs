﻿using Microsoft.Extensions.Logging;

using Netwolf.Server.Commands;
using Netwolf.Server.Internal;
using Netwolf.Server.Users;
using Netwolf.Transport.Commands;
using Netwolf.Transport.Extensions;

using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Sasl;

public class Plain : ISaslMechanismProvider
{
    public string Name => "PLAIN";

    private IAccountProviderFactory AccountProviderFactory { get; init; }
    private IServerPermissionManager ServerPermissionManager { get; init; }
    private ILogger<ISaslMechanismProvider> Logger { get; init; }

    public Plain(IAccountProviderFactory accountProviderFactory, IServerPermissionManager serverPermissionManager, ILogger<ISaslMechanismProvider> logger)
    {
        AccountProviderFactory = accountProviderFactory;
        ServerPermissionManager = serverPermissionManager;
        Logger = logger;
    }

    public ISaslState InitializeForUser(User client)
    {
        return new State(client, AccountProviderFactory, ServerPermissionManager, Logger);
    }

    private class State : ISaslState
    {
        // Should be a multiple of 400
        private const int MAX_SIZE = 1200;

        public string Name => "PLAIN";
        public bool Completed { get; private set; }
        public bool Errored { get; private set; }

        private User Client { get; init; }
        private IAccountProviderFactory AccountProviderFactory { get; init; }
        private IServerPermissionManager ServerPermissionManager { get; init; }
        private ILogger<ISaslMechanismProvider> Logger { get; init; }

        private int MessageCount { get; set; }
        private BoundedBuffer Buffer { get; init; } = new(400, MAX_SIZE);

        public State(User client, IAccountProviderFactory accountProviderFactory, IServerPermissionManager serverPermissionManager, ILogger<ISaslMechanismProvider> logger)
        {
            Client = client;
            AccountProviderFactory = accountProviderFactory;
            ServerPermissionManager = serverPermissionManager;
            Logger = logger;
        }

        // TODO: add logging
        public async Task<ICommandResponse> ProcessClientCommandAsync(ICommand command, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            MessageCount++;
            if (MessageCount == 1)
            {
                return new CommandResponse(Client, null, "AUTHENTICATE", "+");
            }

            if (command.Args[0] != "+")
            {
                if (Buffer.WrittenCount >= MAX_SIZE)
                {
                    // they're sending way too much data, reject them
                    Completed = true;
                    Errored = true;
                    return new NumericResponse(Client, Numeric.ERR_SASLFAIL);
                }

                Encoding.UTF8.GetBytes(command.Args[0], Buffer);
            }

            if (command.Args[0].Length == 400)
            {
                // we're expecting additional data from the client
                return new EmptyResponse();
            }

            // client finished sending data, decode and process it
            if (Base64.DecodeFromUtf8InPlace(Buffer.WrittenSpan, out var bytesWritten) == OperationStatus.InvalidData)
            {
                // invalid base64
                Completed = true;
                Errored = true;
                return new NumericResponse(Client, Numeric.ERR_SASLFAIL);
            }

            Buffer.Reset(bytesWritten);
            // split into spans for authzid, authcid, and password
            byte[] decoded = Buffer.WrittenSpan.ToArray();
            ReadOnlySpan<byte> decodedSpan = decoded.AsSpan();
            int i = 0;
            Range authzid = Range.All,
                authcid = Range.All,
                password = Range.All;

            foreach (var range in decodedSpan.Split((byte)0))
            {
                switch (i)
                {
                    case 0:
                        authzid = range;
                        break;
                    case 1:
                        authcid = range;
                        break;
                    case 2:
                        password = range;
                        break;
                    default:
                        // too many fields, invalid
                        Completed = true;
                        Errored = true;
                        return new NumericResponse(Client, Numeric.ERR_SASLFAIL);
                }

                i++;
            }

            if (i < 3)
            {
                // not enough fields, invalid
                Completed = true;
                Errored = true;
                return new NumericResponse(Client, Numeric.ERR_SASLFAIL);
            }

            IAccountProvider authnProvider = AccountProviderFactory.GetAccountProvider(decoded[authcid]);
            if (!authnProvider.SupportedMechanisms.Contains(AuthMechanism.Password))
            {
                // provider for this user doesn't support password authentication
                Completed = true;
                Errored = true;
                return new NumericResponse(Client, Numeric.ERR_SASLFAIL);
            }

            var authn = authnProvider.NormalizeUsername(decoded[authcid]);

            // only non-null if we're impersonating
            IAccountProvider? authzProvider = null;
            var authz = authn;

            // if authzid is empty, use authcid instead
            if (decoded[0] == 0)
            {
                authzid = authcid;
            }
            else if (!decodedSpan[authcid].SequenceEqual(decodedSpan[authzid]))
            {
                authzProvider = AccountProviderFactory.GetAccountProvider(decodedSpan[authzid]);
                authz = authzProvider.NormalizeUsername(decodedSpan[authzid]);
                if (!authzProvider.SupportedMechanisms.Contains(AuthMechanism.Impersonate)
                    || !Client.HasPrivilege($"oper:impersonate:{authzProvider.ProviderName}:{authz.DecodeUtf8()}"))
                {
                    Completed = true;
                    Errored = true;
                    return new NumericResponse(Client, Numeric.ERR_SASLFAIL);
                }
            }

            var id = await authnProvider.AuthenticatePlainAsync(authn, decoded[password], cancellationToken);
            if (id == null)
            {
                // authentication failed
                Completed = true;
                Errored = true;
                return new NumericResponse(Client, Numeric.ERR_SASLFAIL);
            }
            else if (id.Name == null)
            {
                // invalid response from the account provider
                Completed = true;
                Errored = true;
                return new NumericResponse(Client, Numeric.ERR_SASLFAIL);
            }

            // authentication succeeded
            Client.Account = new UserPrincipal(id);

            if (authzProvider != null)
            {
                // we're impersonating (privileges to do so already checked above)
                // TODO: this should go through some method on User
                id = await authzProvider.ImpersonateAsync(authz, cancellationToken);
                if (id == null)
                {
                    // impersonation failed
                    Completed = true;
                    Errored = true;
                    return new NumericResponse(Client, Numeric.ERR_SASLFAIL);
                }
                else if (id.Name == null)
                {
                    // invalid response from the account provider
                    Completed = true;
                    Errored = true;
                    return new NumericResponse(Client, Numeric.ERR_SASLFAIL);
                }

                Client.Account.AddIdentity(id);
            }

            // TODO: figure out how we want impersonation to work, and also how to extract this from each sasl mech into a common class
            Client.UserPrivileges.Clear();
            foreach (var role in id.Claims.Where(c => c.Type == id.RoleClaimType))
            {
                Client.UserPrivileges.UnionWith(ServerPermissionManager.GetUserPermissionsForRole(authnProvider.ProviderName, role.Value));
            }

            // warn on invalid permissions
            foreach (var priv in Client.UserPrivileges.Where(p => !p.StartsWith("user:")))
            {
                Logger.LogWarning(
                    "User {Username} has invalid permission {Permission} from provider {Provider}",
                    Client.Account.Identity!.Name,
                    priv,
                    authnProvider.ProviderName);
            }

            Completed = true;
            Errored = false;

            // other numerics handled at the caller site? or should this be a MultiResponse that includes the RPL_LOGGEDIN
            return new MultiResponse()
            {
                new NumericResponse(Client, Numeric.RPL_LOGGEDIN, Client.Hostmask, id.Name),
                new NumericResponse(Client, Numeric.RPL_SASLSUCCESS),
            };
        }
    }
}
