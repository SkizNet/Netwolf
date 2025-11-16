using Netwolf.Unicode;

using System.Globalization;
using System.Reflection;
using System.Text;

namespace Netwolf.Test.Unicode;

[TestClass]
public class LineBreakTests
{
    // used by the tests
    const char NO_BREAK = '×';
    const char CAN_BREAK = '÷';

    [DynamicData(nameof(GetLineBreakTestData), DynamicDataDisplayName = nameof(GetLineBreakTestName))]
    [TestMethod]
    public void Test_split_algorithm(string test, string _)
    {
        List<bool> expected = [];
        List<bool> actual = [false];
        StringBuilder sb = new();

        foreach (var part in test.Split(' '))
        {
            if (part[0] == NO_BREAK || part[0] == CAN_BREAK)
            {
                expected.Add(part[0] == CAN_BREAK);
            }
            else
            {
                sb.Append(char.ConvertFromUtf32(int.Parse(part, NumberStyles.AllowHexSpecifier)));
            }
        }

        // this should split on every break opportunity (both optional and mandatory)
        var lines = LineBreakHelper.SplitText(sb.ToString(), 1, true);

        foreach (var (line, _) in lines)
        {
            for (var i = 0; i < line.Length - 1; ++i)
            {
                actual.Add(false);
            }

            actual.Add(true);
        }

        CollectionAssert.AreEqual(expected, actual);
    }

    public static IEnumerable<object[]> GetLineBreakTestData()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var testReader = new StreamReader(assembly.GetManifestResourceStream("Netwolf.Test.Data.LineBreakTest.txt")!);

        string? line;
        while ((line = testReader.ReadLine()) != null)
        {
            if (line.Length == 0 || line[0] == '#')
            {
                // skip blank lines and comments
                continue;
            }

            yield return line.Split('#', 2, StringSplitOptions.TrimEntries);
        }
    }

    public static string GetLineBreakTestName(MethodInfo _, object[] row)
    {
        return row[1].ToString()!;
    }
}
