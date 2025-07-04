using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Internal;

internal class UserPrincipal : ClaimsPrincipal
{
    public UserPrincipal(IIdentity identity) : base(identity) { }

    public override IIdentity? Identity
    {
        get
        {
            // we explicitly ignore PrimaryIdentitySelector here because it's static, and the application
            // may have customized it for their own uses for their own ClaimsPrincipals. Instead, we
            // first attempt to return an Impersonated identity, otherwise we'll return the first ClaimsIdentity
            // that has a Name and AuthenticationType set.
            return Identities.OfType<ClaimsIdentity>().FirstOrDefault(i => !string.IsNullOrEmpty(i.Name) && i.AuthenticationType == "Impersonate")
                ?? Identities.OfType<ClaimsIdentity>().FirstOrDefault(i => !string.IsNullOrEmpty(i.Name) && i.IsAuthenticated);
        }
    }
}
