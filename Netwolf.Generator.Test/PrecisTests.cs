﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Netwolf.Generator.PRECIS;

using System.Reflection;
using System.Runtime.InteropServices;

namespace Netwolf.Generator.Test;

[TestClass]
public class PrecisTests
{
    [TestMethod]
    public void Successfully_generates_UnicodeProperty()
    {
        using var bidiReader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("Netwolf.Generator.Test.BidiClass.cs")!);
        using var combReader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("Netwolf.Generator.Test.CombiningClass.cs")!);
        using var hangReader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("Netwolf.Generator.Test.HangulSyllableType.cs")!);
        using var joinReader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("Netwolf.Generator.Test.JoiningType.cs")!);
        using var scriptReader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("Netwolf.Generator.Test.Script.cs")!);

        // compilation object we inject our source into containing the implementation of partial methods the generator requires
        Compilation input = CSharpCompilation.Create("Netwolf.PRECIS",
            [
                CSharpSyntaxTree.ParseText(@"global using global::System; global using global::System.Collections.Generic;"),
                CSharpSyntaxTree.ParseText(bidiReader.ReadToEnd()),
                CSharpSyntaxTree.ParseText(combReader.ReadToEnd()),
                CSharpSyntaxTree.ParseText(hangReader.ReadToEnd()),
                CSharpSyntaxTree.ParseText(joinReader.ReadToEnd()),
                CSharpSyntaxTree.ParseText(scriptReader.ReadToEnd()),
                CSharpSyntaxTree.ParseText(@"
using System.Text;

namespace Netwolf.PRECIS.Internal
{
    internal static partial class UnicodeProperty
    {
        internal static partial bool IsJoinControl(Rune rune);
        internal static partial bool IsNoncharacterCodePoint(Rune rune);
        internal static partial bool IsDefaultIgnorableCodePoint(Rune rune);
        internal static partial BidiClass GetBidiClass(Rune rune);
        internal static partial HangulSyllableType GetHangulSyllableType(Rune rune);
        internal static partial CombiningClass GetCombiningClass(Rune rune);
        internal static partial JoiningType GetJoiningType(Rune rune);
        internal static partial Script GetScript(Rune rune);

        private static T? GeneratedDatabaseLookup<T>(int value, List<Tuple<int, int, T>> database)
            where T : struct
        {
            return null;
        }
    }
}"),
            ],
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            ],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        PrecisDataGenerator generator = new();
        CSharpGeneratorDriver.Create(generator)
            .AddAdditionalTexts([
                new EmbeddedAdditionalText("DerivedBidiClass.txt"),
                new EmbeddedAdditionalText("DerivedCombiningClass.txt"),
                new EmbeddedAdditionalText("DerivedCoreProperties.txt"),
                new EmbeddedAdditionalText("DerivedJoiningType.txt"),
                new EmbeddedAdditionalText("HangulSyllableType.txt"),
                new EmbeddedAdditionalText("PropList.txt"),
                new EmbeddedAdditionalText("Scripts.txt"),
                ])
            .RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        Assert.IsEmpty(diagnostics, "Diagnostics were created by the source generator.");

        var outputDiagnostics = output.GetDiagnostics();
        Assert.IsEmpty(outputDiagnostics, "Diagnostics were created while compiling generated source.");
        // 7 source files specified above, plus 6 generated files (PropList and DerivedCoreProperties both go to the same file)
        Assert.AreEqual(13, output.SyntaxTrees.Count());

        using MemoryStream assemblyStream = new();
        output.Emit(assemblyStream);
        assemblyStream.Seek(0, SeekOrigin.Begin);

        using MetadataLoadContext mlc = new(new PathAssemblyResolver(Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll")));
        var assembly = mlc.LoadFromByteArray(assemblyStream.ToArray());
        var type = assembly.GetType("Netwolf.PRECIS.Internal.UnicodeProperty");
        Assert.IsNotNull(type);
    }

    [TestMethod]
    public void Successfully_generates_DecompositionMappings()
    {
        // compilation object we inject our source into containing the implementation of partial methods the generator requires
        Compilation input = CSharpCompilation.Create("Netwolf.PRECIS",
            [
                CSharpSyntaxTree.ParseText(@"global using global::System.Collections.Generic;"),
                CSharpSyntaxTree.ParseText(@"
namespace Netwolf.PRECIS.Internal
{
    internal static partial class DecompositionMappings
    {
        private static partial IEnumerable<KeyValuePair<int, int[]>> GetWideMappings();
        private static partial IEnumerable<KeyValuePair<int, int[]>> GetNarrowMappings();
    }
}"),
            ],
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            ],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        PrecisDataGenerator generator = new();
        CSharpGeneratorDriver.Create(generator)
            .AddAdditionalTexts([new EmbeddedAdditionalText("UnicodeData.txt")])
            .RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        Assert.IsEmpty(diagnostics, "Diagnostics were created by the source generator.");

        var outputDiagnostics = output.GetDiagnostics();
        Assert.IsEmpty(outputDiagnostics, "Diagnostics were created while compiling generated source.");
        Assert.AreEqual(3, output.SyntaxTrees.Count());

        using MemoryStream assemblyStream = new();
        output.Emit(assemblyStream);
        assemblyStream.Seek(0, SeekOrigin.Begin);

        using MetadataLoadContext mlc = new(new PathAssemblyResolver(Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll")));
        var assembly = mlc.LoadFromByteArray(assemblyStream.ToArray());
        var type = assembly.GetType("Netwolf.PRECIS.Internal.DecompositionMappings");
        Assert.IsNotNull(type);
    }
}
