using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Users;

public enum AuthMechanism
{
    Password,
    Scram,
    ClientCertificate,
    PublicKeyChallenge,
    OAuthBearerToken,
    OpenIdConnect,
}
