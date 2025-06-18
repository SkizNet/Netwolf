using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Netwolf.Generator.Transport;

using System.Reflection;
using System.Runtime.InteropServices;

namespace Netwolf.Generator.Test;

[TestClass]
public class UnicodeHelperTests
{
    [TestMethod]
    public void Successfully_generates_source()
    {
        using var helperReader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("Netwolf.Generator.Test.UnicodeHelper.cs")!);
        using var extensionsReader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("Netwolf.Generator.Test.UnicodeExtensions.cs")!);

        // compilation object we inject our source into containing the implementation of partial methods the generator requires
        Compilation input = CSharpCompilation.Create("Netwolf.Transport",
            [
                CSharpSyntaxTree.ParseText(@"global using global::System; global using global::System.Collections.Generic; global using global::System.Linq;"),
                CSharpSyntaxTree.ParseText(helperReader.ReadToEnd()),
                CSharpSyntaxTree.ParseText(extensionsReader.ReadToEnd()),
            ],
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                // we need a ref to System.Runtime as well but there's no types directly defined in it
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location.Replace("System.Private.CoreLib.dll", "System.Runtime.dll")),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            ],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        UnicodeHelperGenerator generator = new();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(input, out var output, out var diagnostics);
        Assert.IsEmpty(diagnostics, "Diagnostics were created by the source generator.");

        var outputDiagnostics = output.GetDiagnostics();
        Assert.IsEmpty(outputDiagnostics, "Diagnostics were created while compiling generated source.");
        Assert.AreEqual(4, output.SyntaxTrees.Count());

        using MemoryStream assemblyStream = new();
        output.Emit(assemblyStream);
        assemblyStream.Seek(0, SeekOrigin.Begin);

        using MetadataLoadContext mlc = new(new PathAssemblyResolver(Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll")));
        var assembly = mlc.LoadFromByteArray(assemblyStream.ToArray());
        var type = assembly.GetType("Netwolf.Transport.Internal.UnicodeHelper");
        Assert.IsNotNull(type);

        // GeneratedDatabaseLookup only exists in the generated source file, so checking for it indicates source generator worked as expected
        var method = type.GetMethod("GeneratedDatabaseLookup", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method);
        Assert.IsTrue(method.IsGenericMethod);
    }
}
