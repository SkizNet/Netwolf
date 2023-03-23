using Netwolf.Server.Internal;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server
{
    public class User
    {
        public HashSet<string> UserPrivileges { get; init; } = new();

        public HashSet<string> OperPrivileges { get; init; } = new();

        public bool HasPrivilege(string priv, Channel? channel = null)
        {
            if (priv.Split(':', 2) is not [string scope, _])
            {
                throw new ArgumentException("Invalid privilege, expected scope:what", nameof(priv));
            }

            HashSet<string> container = scope switch
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
            foreach (var piece in priv.Split(':').Skip(1))
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
    }
}
