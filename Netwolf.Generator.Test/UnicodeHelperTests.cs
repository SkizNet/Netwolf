using Netwolf.Generator.Transport;

namespace Netwolf.Generator.Test;

[TestClass]
public class UnicodeHelperTests
{
    [TestMethod]
    public void Successfully_generates_source()
    {
        var sb = UnicodeHelperGenerator.GetSourceText();

        // TODO: lint the file for syntax errors or something?
        Assert.IsTrue(sb.Length > 0);
    }
}
