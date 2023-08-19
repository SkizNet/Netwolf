using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Users;

public interface IUserFactory
{
    User Create(IPAddress ip, int localPort, int remotePort);
}
