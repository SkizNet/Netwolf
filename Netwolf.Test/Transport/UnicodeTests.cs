using Netwolf.Transport.Internal;

using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Netwolf.Test.Transport;

[TestClass]
public class UnicodeTests
{
    // used by the tests
    const char NO_BREAK = '×';
    const char CAN_BREAK = '÷';

    [DynamicData(nameof(GetLineBreakTestData), DynamicDataSourceType.Method, DynamicDataDisplayName = nameof(GetLineBreakTestName))]
    [DataTestMethod]
    public void Test_split_algorithm(string test, string comment)
    {
        List<bool> expected = new();
        List<bool> actual = new();
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
        var lines = UnicodeHelper.SplitText(sb.ToString(), 1, true);

        actual.Add(false);
        foreach (var line in lines)
        {
            for (var i = 0; i < line.Length - 1; ++i)
            {
                actual.Add(false);
            }

            actual.Add(true);
        }
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

    public static string GetLineBreakTestName(MethodInfo info, object[] row)
    {
        return row[1].ToString()!;
    }
}
