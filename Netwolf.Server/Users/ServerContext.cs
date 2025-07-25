using Netwolf.Transport.Context;
using Netwolf.Transport.IRC;
using Netwolf.Transport.State;

namespace Netwolf.Server.Users;

// TODO: this is a bad design and we should operate on IContext elsewhere in Netwolf.Server
// the only creator of these objects is in unit tests right now
public class ServerContext : ExtensibleContextBase
{
    public User? User { get; set; }

    public Channel? Channel { get; set; }

    public required object Server { get; set; }

    public override object Sender => Server;

    protected override ChannelRecord? GetContextChannel()
    {
        throw new NotImplementedException();
    }

    protected override INetworkInfo GetContextNetwork()
    {
        throw new NotImplementedException();
    }

    protected override UserRecord? GetContextUser()
    {
        throw new NotImplementedException();
    }
}
