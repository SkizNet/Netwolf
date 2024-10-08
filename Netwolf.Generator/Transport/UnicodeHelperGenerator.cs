﻿using Microsoft.CodeAnalysis;

using System.Reflection;
using System.Text;

using DatabaseRecord = System.Tuple<string, string, string>;

namespace Netwolf.Generator.Transport;

[Generator]
public class UnicodeHelperGenerator : IIncrementalGenerator
{
    public static StringBuilder GetSourceText()
    {
        var sb = new StringBuilder();
        string line;

        var assembly = Assembly.GetExecutingAssembly();

        using var lineBreakReader = new StreamReader(assembly.GetManifestResourceStream("Netwolf.Generator.Data.LineBreak.txt"));
        using var eastAsianWidthReader = new StreamReader(assembly.GetManifestResourceStream("Netwolf.Generator.Data.EastAsianWidth.txt"));
        var lineBreaks = new List<DatabaseRecord>();
        var eastAsianWidths = new List<DatabaseRecord>();

        var databases = new StreamReader[] { lineBreakReader, eastAsianWidthReader };
        var lists = new List<DatabaseRecord>[] { lineBreaks, eastAsianWidths };

        for (int i = 0; i < databases.Length; ++i)
        {
            var database = databases[i];
            var list = lists[i];

            while ((line = database.ReadLine()) != null)
            {
                if (line.Length == 0 || line[0] == '#')
                {
                    // skip blank lines and comments
                    continue;
                }

                // take data up to first #, in the format range;class
                string[] data = line.Substring(0, line.IndexOf('#')).Split(';');
                if (data[0].IndexOf('.') != -1)
                {
                    // have a range
                    string[] range = data[0].Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
                    list.Add(new DatabaseRecord(range[0].Trim(), range[1].Trim(), data[1].Trim()));
                }
                else
                {
                    // no range
                    list.Add(new DatabaseRecord(data[0].Trim(), data[0].Trim(), data[1].Trim()));
                }
            }
        }

        sb.Append(@"// <auto-generated/>
using System.Text;

namespace Netwolf.Transport.Internal
{
    internal static partial class UnicodeHelper
    {
        private static partial LineBreakClass GetLineBreakClass(Rune rune)
        {
            return GeneratedDatabaseLookup<LineBreakClass>(rune.Value, GeneratedLineBreakClasses, LineBreakClass.XX);
        }

        private static partial EastAsianWidth GetEastAsianWidth(Rune rune)
        {
            return GeneratedDatabaseLookup<EastAsianWidth>(rune.Value, GeneratedEastAsianWidths, EastAsianWidth.N);
        }

        private static T GeneratedDatabaseLookup<T>(int value, List<Tuple<int, int, T>> database, T defaultValue)
        {
            int start = 0;
            int end = database.Count;
            int cur = end >> 1;

            if (value < 0 || value > 0x10FFFD)
            {
                // outside of unicode range; should never happen (and indicates a bug somewhere if it does)
                throw new ArgumentException(""Value outside of Unicode range (0x0000 - 0x10FFFD)"", nameof(value));
            }

            while (end > start)
            {
                if (value < database[cur].Item1)
                {
                    end = cur - 1;
                }
                else if (value > database[cur].Item2)
                {
                    start = cur + 1;
                }
                else
                {
                    return database[cur].Item3;
                }

                cur = (start + end) >> 1;
            }

            return defaultValue;
        }

        private static readonly List<Tuple<int, int, LineBreakClass>> GeneratedLineBreakClasses = new()
        {
");
        foreach (var item in lineBreaks)
        {
            sb.AppendLine($"            new Tuple<int, int, LineBreakClass>(0x{item.Item1}, 0x{item.Item2}, LineBreakClass.{item.Item3}),");
        }

        sb.Append(@"
        };

        private static readonly List<Tuple<int, int, EastAsianWidth>> GeneratedEastAsianWidths = new()
        {
");
        foreach (var item in eastAsianWidths)
        {
            sb.AppendLine($"            new Tuple<int, int, EastAsianWidth>(0x{item.Item1}, 0x{item.Item2}, EastAsianWidth.{item.Item3}),");
        }

        sb.Append(@"
        };
    }
}");

        return sb;
    }

    public void Execute(SourceProductionContext context, string? assemblyName)
    {
        // only execute in Netwolf.Transport
        if (assemblyName != "Netwolf.Transport")
        {
            return;
        }

        context.AddSource("UnicodeHelper.g.cs", GetSourceText().ToString());
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var provider = context.CompilationProvider.Select(static (compilation, _) => compilation.AssemblyName);

        context.RegisterSourceOutput(provider, Execute);
    }
}
