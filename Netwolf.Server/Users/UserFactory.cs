using Microsoft.Extensions.DependencyInjection;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Users;

public class UserFactory : IUserFactory
{
    private IServiceProvider ServiceProvider { get; init; }

    public UserFactory(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
    }

    public User Create(IPAddress ip, int localPort, int remotePort)
    {
        return ActivatorUtilities.CreateInstance<User>(ServiceProvider, ip, localPort, remotePort);
    }
}
