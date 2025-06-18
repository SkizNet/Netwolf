using Netwolf.Transport.Context;

namespace Netwolf.Server.Users;

public class ServerContext : ExtensibleContextBase
{
    public User? User { get; set; }

    public Channel? Channel { get; set; }

    public required object Server { get; set; }

    public override object Sender => Server;
}
