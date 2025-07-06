using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Netwolf.Generator.PRECIS;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Generator.Test;

[TestClass]
public class DecompositionMappingTests
{
    [TestMethod]
    public void Successfully_generates_source()
    {
        // compilation object we inject our source into containing the implementation of partial methods the generator requires
        Compilation input = CSharpCompilation.Create("Netwolf.PRECIS",
            [
                CSharpSyntaxTree.ParseText(@"global using global::System.Collections.Generic;"),
                CSharpSyntaxTree.ParseText(GetSkeleton()),
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

    private static string GetSkeleton()
    {
        return @"
namespace Netwolf.PRECIS.Internal
{
    internal static partial class DecompositionMappings
    {
        private static partial IEnumerable<KeyValuePair<int, int[]>> GetWideMappings();
        private static partial IEnumerable<KeyValuePair<int, int[]>> GetNarrowMappings();
    }
}";
    }
}

