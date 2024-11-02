using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using Netwolf.Transport.Extensions.DependencyInjection;
using Netwolf.Transport.IRC;
using Netwolf.Transport.IRC.Fakes;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Test.Transport;

[TestClass]
public class StateTests
{
    private ServiceProvider Container { get; init; }
    private static NetworkOptions Options => new()
    {
        PrimaryNick = "test",
        Ident = "id",
        RealName = "real name",
    };

    public StateTests()
    {
        Container = new ServiceCollection()
            .AddLogging(config => config.SetMinimumLevel(LogLevel.Debug).AddConsole())
            // bring in default Netwolf DI services
            .AddTransportServices()
            .Replace(ServiceDescriptor.Singleton<IConnectionFactory>(new StubIConnectionFactory()
            {
                CreateINetworkIServerNetworkOptions = (name, server, options) => new StubIConnection()
            }))
            .BuildServiceProvider();
    }

    [TestMethod]
    public void Successfully_self_join_channel()
    {
        using var scope = Container.CreateScope();
        var networkFactory = Container.GetRequiredService<INetworkFactory>();
        using var network = (Network)networkFactory.Create("NetwolfTest", Options);
        network.RegisterForUnitTests(new StubIServer(), "127.0.0.1", "acct");

        network.ReceiveLineForUnitTests(":test!id@127.0.0.1 JOIN #TestiNg");
        var channel = network.GetChannel("#testing");
        Assert.IsNotNull(channel);
        Assert.AreEqual("#TestiNg", channel.Name);
        Assert.IsTrue(network.Channels.ContainsKey(channel));
        Assert.AreEqual(network.ClientId, channel.Users.Single().Key);
        Assert.AreEqual(string.Empty, channel.Users.Single().Value);
    }

    [TestMethod]
    public void Successfully_other_join_channel_regular()
    {
        using var scope = Container.CreateScope();
        var networkFactory = Container.GetRequiredService<INetworkFactory>();
        using var network = (Network)networkFactory.Create("NetwolfTest", Options);
        network.RegisterForUnitTests(new StubIServer(), "127.0.0.1", "acct");

        network.ReceiveLineForUnitTests(":test!id@127.0.0.1 JOIN #TestiNg");
        network.ReceiveLineForUnitTests(":foo!~bar@baz/baz JOIN #TestiNg");
        var channel = network.GetChannel("#testing");
        var user = network.GetUserByNick("FOO");
        Assert.IsNotNull(channel);
        Assert.IsNotNull(user);
        Assert.AreEqual(2, channel.Users.Count);
        Assert.AreEqual("foo", user.Nick);
        Assert.AreEqual("~bar", user.Ident);
        Assert.AreEqual("baz/baz", user.Host);
        Assert.IsNull(user.Account);
        Assert.AreEqual(string.Empty, user.RealName);
        Assert.IsTrue(channel.Users.ContainsKey(user.Id));
        Assert.IsTrue(user.Channels.ContainsKey(channel.Id));
        Assert.AreEqual(string.Empty, channel.Users[user.Id]);
        Assert.AreEqual(string.Empty, user.Channels[channel.Id]);
    }

    [TestMethod]
    public void Successfully_other_join_channel_extended()
    {
        using var scope = Container.CreateScope();
        var networkFactory = Container.GetRequiredService<INetworkFactory>();
        using var network = (Network)networkFactory.Create("NetwolfTest", Options);
        network.RegisterForUnitTests(new StubIServer(), "127.0.0.1", "acct");

        network.ReceiveLineForUnitTests(":irc.netwolf.org CAP test ACK :extended-join");
        network.ReceiveLineForUnitTests(":test!id@127.0.0.1 JOIN #TestiNg different :also different");
        network.ReceiveLineForUnitTests(":foo!~bar@baz/baz JOIN #TestiNg * :UwU");
        var channel = network.GetChannel("#testing");
        var user = network.GetUserByNick("FOO");
        Assert.IsNotNull(channel);
        Assert.IsNotNull(user);
        Assert.AreEqual(2, channel.Users.Count);
        Assert.AreEqual("foo", user.Nick);
        Assert.AreEqual("~bar", user.Ident);
        Assert.AreEqual("baz/baz", user.Host);
        Assert.IsNull(user.Account);
        Assert.AreEqual("UwU", user.RealName);
        Assert.IsTrue(channel.Users.ContainsKey(user.Id));
        Assert.IsTrue(user.Channels.ContainsKey(channel.Id));
        Assert.AreEqual(string.Empty, channel.Users[user.Id]);
        Assert.AreEqual(string.Empty, user.Channels[channel.Id]);

        // ensure that our details were updated too
        Assert.AreEqual("different", network.Account);
        Assert.AreEqual("also different", network.RealName);
    }

    [TestMethod]
    public void Successfully_ignore_join_for_unjoined_channel()
    {
        using var scope = Container.CreateScope();
        var networkFactory = Container.GetRequiredService<INetworkFactory>();
        using var network = (Network)networkFactory.Create("NetwolfTest", Options);
        network.RegisterForUnitTests(new StubIServer(), "127.0.0.1", "acct");

        network.ReceiveLineForUnitTests(":foo!~bar@baz/baz JOIN #testing");
        var channel = network.GetChannel("#testing");
        var user = network.GetUserByNick("foo");
        Assert.IsNull(channel);
        Assert.IsNull(user);
    }

    [DataTestMethod]
    [DataRow(":a!~a@a.a PART #testing", DisplayName = "PART without reason")]
    [DataRow(":a!~a@a.a PART #testing,#other", DisplayName = "PART multiple channels")]
    [DataRow(":a!~a@a.a PART #testing :part reason", DisplayName = "PART with reason")]
    [DataRow(":c!~c@c.c KICK #testing a", DisplayName = "KICK without reason")]
    [DataRow(":c!~c@c.c KICK #testing a :kick reason", DisplayName = "KICK with reason")]
    [DataRow(":a!~a@a.a QUIT", DisplayName = "QUIT without reason")]
    [DataRow(":a!~a@a.a QUIT :quit reason", DisplayName = "QUIT with reason")]
    public void Successfully_remove_other_from_channel(string line)
    {
        using var scope = Container.CreateScope();
        var networkFactory = Container.GetRequiredService<INetworkFactory>();
        using var network = (Network)networkFactory.Create("NetwolfTest", Options);
        network.RegisterForUnitTests(new StubIServer(), "127.0.0.1", "acct");

        network.ReceiveLineForUnitTests(":test!id@127.0.0.1 JOIN #testing");
        network.ReceiveLineForUnitTests(":a!~a@a.a JOIN #testing");
        network.ReceiveLineForUnitTests(":b!~b@b.b JOIN #testing");
        network.ReceiveLineForUnitTests(":c!~c@c.c JOIN #testing");

        // line should remove a from the channel via various methods
        network.ReceiveLineForUnitTests(line);

        var channel = network.GetChannel("#testing");
        Assert.IsNotNull(channel);
        Assert.IsNull(network.GetUserByNick("a"));
        Assert.AreEqual(3, channel.Users.Count);
    }

    [DataTestMethod]
    [DataRow(":test!id@127.0.0.1 PART #testing", DisplayName = "PART without reason")]
    [DataRow(":test!id@127.0.0.1 PART #testing,#other", DisplayName = "PART multiple channels")]
    [DataRow(":test!id@127.0.0.1 PART #testing :part reason", DisplayName = "PART with reason")]
    [DataRow(":c!~c@c.c KICK #testing test", DisplayName = "KICK without reason")]
    [DataRow(":c!~c@c.c KICK #testing test :kick reason", DisplayName = "KICK with reason")]
    public void Successfully_remove_self_from_channel(string line)
    {
        using var scope = Container.CreateScope();
        var networkFactory = Container.GetRequiredService<INetworkFactory>();
        using var network = (Network)networkFactory.Create("NetwolfTest", Options);
        network.RegisterForUnitTests(new StubIServer(), "127.0.0.1", "acct");

        network.ReceiveLineForUnitTests(":test!id@127.0.0.1 JOIN #testing");
        network.ReceiveLineForUnitTests(":a!~a@a.a JOIN #testing");
        network.ReceiveLineForUnitTests(":b!~b@b.b JOIN #testing");
        network.ReceiveLineForUnitTests(":c!~c@c.c JOIN #testing");

        // line should remove a from the channel via various methods
        network.ReceiveLineForUnitTests(line);

        Assert.IsNull(network.GetChannel("#testing"));
        Assert.IsNull(network.GetUserByNick("a"));
    }

    [TestMethod]
    public void Successfully_change_modes()
    {
        using var scope = Container.CreateScope();
        var networkFactory = Container.GetRequiredService<INetworkFactory>();
        using var network = (Network)networkFactory.Create("NetwolfTest", Options);
        network.RegisterForUnitTests(new StubIServer(), "127.0.0.1", "acct");

        network.ReceiveLineForUnitTests(":test!id@127.0.0.1 JOIN #testing");
        network.ReceiveLineForUnitTests(":a!~a@a.a JOIN #testing");
        network.ReceiveLineForUnitTests(":b!~b@b.b JOIN #testing");
        network.ReceiveLineForUnitTests(":c!~c@c.c JOIN #testing");

        network.ReceiveLineForUnitTests(":irc.netwolf.org MODE #testing +iobl a d!*@* 5");
        network.ReceiveLineForUnitTests(":irc.netwolf.org MODE #testing +vv-vv test a b c");

        var channel = network.GetChannel("#testing")!;
        var users = network.GetUsersInChannel(channel);
        var testUser = users.Single(u => u.Key.Nick == "test").Key;
        var aUser = users.Single(u => u.Key.Nick == "a").Key;
        var bUser = users.Single(u => u.Key.Nick == "b").Key;

        Assert.AreEqual("+", users[testUser]);
        Assert.AreEqual("+", channel.Users[testUser.Id]);
        Assert.AreEqual("+", testUser.Channels[channel.Id]);
        Assert.AreEqual("@+", users[aUser]);
        Assert.AreEqual("@+", channel.Users[aUser.Id]);
        Assert.AreEqual("@+", aUser.Channels[channel.Id]);
        Assert.AreEqual(string.Empty, users[bUser]);
        Assert.AreEqual(string.Empty, channel.Users[bUser.Id]);
        Assert.AreEqual(string.Empty, bUser.Channels[channel.Id]);

        network.ReceiveLineForUnitTests(":irc.netwolf.org MODE #testing +k-o pw a");

        // refresh channel data since records are immutable
        channel = network.GetChannel("#testing")!;
        users = network.GetUsersInChannel(channel);
        aUser = users.Single(u => u.Key.Nick == "a").Key;

        Assert.AreEqual("+", users[aUser]);
        Assert.AreEqual("+", channel.Users[aUser.Id]);
        Assert.AreEqual("+", aUser.Channels[channel.Id]);
        Assert.AreEqual("ikl", string.Concat(channel.Modes.Keys.OrderBy(c => c)));
        Assert.AreEqual("pw", channel.Modes['k']);
        Assert.AreEqual("5", channel.Modes['l']);
        Assert.IsNull(channel.Modes['i']);
    }
}
