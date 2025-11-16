using Netwolf.Transport.Sasl;
using Netwolf.Unicode;

namespace Netwolf.Test.Transport;

[TestClass]
public class SaslTests
{
    [TestMethod]
    [DataRow("foo", null, "bar", "\u0000foo\u0000bar", DisplayName = "Without authz")]
    [DataRow("foo", "baz", "bar", "baz\u0000foo\u0000bar", DisplayName = "With authz")]
    public void Successfully_form_message_plain(string authn, string? authz, string password, string expected)
    {
        var sasl = new SaslPlain(authn, authz, password);
        Assert.IsTrue(sasl.Authenticate([], out var response));
        Assert.AreEqual(expected, response.DecodeUtf8());
    }

    [TestMethod]
    [DataRow(null, "", DisplayName = "Without authz")]
    [DataRow("foo", "foo", DisplayName = "With authz")]
    public void Successfully_form_message_external(string? authz, string expected)
    {
        var sasl = new SaslExternal(authz);
        Assert.IsTrue(sasl.Authenticate([], out var response));
        Assert.AreEqual(expected, response.DecodeUtf8());
    }
}
