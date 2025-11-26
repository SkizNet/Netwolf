using Netwolf.Transport.MFA;
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

    [TestMethod]
    [DataRow("SHA-1", "user", null, "pencil", "fyko+d2lbbFgONRv9qkxdawL",
        "n,,n=user,r=fyko+d2lbbFgONRv9qkxdawL",
        "r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,s=QSXCR+Q6sek8bf92,i=4096",
        "c=biws,r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,p=v0X8v3Bz2T0CJGbJQyF0X+HI4Ts=",
        "v=rmF9pqV8S7suAoZWja4dJRkFsKQ=",
        DisplayName = "SHA-1 RFC 5802 example")]
    [DataRow("SHA-256", "user", null, "pencil", "rOprNGfwEbeRWgbNEkqO",
        "n,,n=user,r=rOprNGfwEbeRWgbNEkqO",
        "r=rOprNGfwEbeRWgbNEkqO%hvYDpWUa2RaTCAfuxFIlj)hNlF$k0,s=W22ZaJ0SNY7soEsUEjb6gQ==,i=4096",
        "c=biws,r=rOprNGfwEbeRWgbNEkqO%hvYDpWUa2RaTCAfuxFIlj)hNlF$k0,p=dHzbZapWIk4jUhN+Ute9ytag9zjfMHgsqmmiz7AndVQ=",
        "v=6rriTRBi23WpRR/wtup+mMhUZUn/dB5nLTJRsjl95G4=",
        DisplayName = "SHA-256 RFC 7677 example")]
    public void Successfully_form_messages_scram(
        string hash,
        string authn,
        string? authz,
        string password,
        string clientNonce,
        string expectedClientFirst,
        string serverFirst,
        string expectedClientFinal,
        string serverFinal)
    {
        var sasl = new SaslScram(hash, authn, authz, password);
        sasl.SetNonceForUnitTests(clientNonce);
        sasl.Authenticate([], out var response);
        Assert.AreEqual(expectedClientFirst, response.DecodeUtf8());
        sasl.Authenticate(serverFirst.EncodeUtf8(), out response);
        Assert.AreEqual(expectedClientFinal, response.DecodeUtf8());
        Assert.IsTrue(sasl.Authenticate(serverFinal.EncodeUtf8(), out _));
    }
}
