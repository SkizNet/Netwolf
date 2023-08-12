using Netwolf.Server.Internal;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Test.Server;

[TestClass]
public class GlobTests
{
    [TestMethod]
    public void No_wildcards()
    {
        var glob = Glob.For("foobar");
        Assert.IsTrue(glob.IsMatch("foobar"));
        Assert.IsFalse(glob.IsMatch("foo"));
        Assert.IsFalse(glob.IsMatch("bar"));
        Assert.IsFalse(glob.IsMatch("barfoo"));
        Assert.IsFalse(glob.IsMatch("oobar"));
        Assert.IsFalse(glob.IsMatch("fooba"));
        Assert.IsFalse(glob.IsMatch("foobar2"));
        Assert.IsFalse(glob.IsMatch(String.Empty));
    }

    [TestMethod]
    public void Question_mark_in_string()
    {
        var glob = Glob.For("fo??ar");
        Assert.IsTrue(glob.IsMatch("foobar"));
        Assert.IsTrue(glob.IsMatch("foboar"));
        Assert.IsFalse(glob.IsMatch("foobarbaz"));
        Assert.IsFalse(glob.IsMatch("fobar"));
        Assert.IsFalse(glob.IsMatch("foarba"));
        Assert.IsFalse(glob.IsMatch("fo"));
        Assert.IsFalse(glob.IsMatch(String.Empty));
    }

    [TestMethod]
    public void Question_mark_only()
    {
        var glob = Glob.For("??");
        Assert.IsTrue(glob.IsMatch("fo"));
        Assert.IsTrue(glob.IsMatch("ba"));
        Assert.IsFalse(glob.IsMatch("foobar"));
        Assert.IsFalse(glob.IsMatch("f"));
        Assert.IsFalse(glob.IsMatch(String.Empty));
    }

    [TestMethod]
    public void Star_in_string()
    {
        var glob = Glob.For("foo*bar");
        Assert.IsTrue(glob.IsMatch("foobar"));
        Assert.IsTrue(glob.IsMatch("fooobar"));
        Assert.IsTrue(glob.IsMatch("foobazbar"));
        Assert.IsTrue(glob.IsMatch("foofoobar"));
        Assert.IsTrue(glob.IsMatch("foobarbar"));
        Assert.IsTrue(glob.IsMatch("foofoobarbar"));
        Assert.IsFalse(glob.IsMatch("fobar"));
        Assert.IsFalse(glob.IsMatch("fooooba"));
        Assert.IsFalse(glob.IsMatch(String.Empty));
    }
}
