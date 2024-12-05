// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Netwolf.Generator.Internal;

using System.Collections.Immutable;

namespace Netwolf.Generator.PluginFramework;

[Generator]
public class PluginCommandGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var commands = context.SyntaxProvider
            .ForAttributeWithMetadataName("Netwolf.Attributes.CommandAttribute", IsValidCommand, TransformCommand)
            .Where(c => c.ContextType != ContextType.Invalid);

        context.RegisterImplementationSourceOutput(commands, GenerateCommandFile);
    }

    private bool IsValidCommand(SyntaxNode node, CancellationToken token)
    {
        // can't do full verification without symbol resolution so just return true for public methods and sort out FPs later
        return node is MethodDeclarationSyntax method && method.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword));
    }

    private CommandContext TransformCommand(GeneratorAttributeSyntaxContext syntaxContext, CancellationToken token)
    {
        INamedTypeSymbol? classType = syntaxContext.TargetSymbol.ContainingType;
        while (classType != null && classType.ToFullyQualifiedString() != "Netwolf.BotFramework.Bot")
        {
            classType = classType.BaseType;
        }

        if (classType == null || syntaxContext.TargetSymbol is not IMethodSymbol method)
        {
            // not a bot command, which is the only command type we support at the moment
            return CommandContext.Invalid;
        }

        var attributes = syntaxContext.Attributes
            .Select(attr => new CommandAttributeContext(
                attr.ConstructorArguments[0].Value as string,
                attr.ApplicationSyntaxReference?.GetSyntax(token).GetLocation(),
                // this is a syntactically valid C# string, i.e. it includes the surrounding quotes
                attr.ConstructorArguments[0].ToCSharpString(),
                // default(TypedConstant) sets value to null, which gives "null" below despite Kind == TypedConstantKind.Error
                attr.ConstructorArguments.ElementAtOrDefault(1).ToCSharpString()))
            .ToImmutableArray();

        if (attributes.Length == 0)
        {
            // should never happen, but it's better to not throw an exception from a source generator because it's *really* obnoxious to debug them
            return CommandContext.Invalid;
        }

        // rather than checking if the method is declared async, we care more if it retuns an awaitable type
        // instead of doing an exhaustive check here, simply detect if the return type has a GetAwaiter() method or if the method is async void
        // this means that we aren't fully testing the await contract here which could lead to false positives and compilation errors later,
        // but any such errors would be due to the end user doing something extremely stupid so it's not worth being thorough
        bool isAsync = method.IsAsync || method.ReturnType.GetMembers("GetAwaiter").OfType<IMethodSymbol>().Any();

        // check for a void return type, or for async methods, also if the return type's GetAwaiter().GetResult() returns void
        bool isVoid = method.ReturnsVoid;

        if (!isVoid && isAsync)
        {
            // if we can't find GetAwaiter or GetResult (perhaps they're extension methods), assume that the result is void
            // it's not a syntax error to ignore the result of an await if it has one,
            // but it *is* a syntax error to attempt to capture the result when it doesn't have one
            isVoid = method.ReturnType
                .GetMembers("GetAwaiter").OfType<IMethodSymbol>().FirstOrDefault()?.ReturnType
                .GetMembers("GetResult").OfType<IMethodSymbol>().FirstOrDefault()?.ReturnsVoid
                ?? true;
        }

        // fetch param details
        var parameters = method.Parameters
            .Select(param => new CommandParameterContext(
                param.Name,
                param.Type.ToFullyQualifiedString(),
                GetParameterClassification(param),
                GetConversionTemplate(syntaxContext.SemanticModel.Compilation, param.Type, GetDefaultValue(param)),
                param.HasExplicitDefaultValue,
                GetDefaultValue(param),
                param.Locations[0]))
            .ToImmutableArray();

        return new(ContextType.Bot,
            method.ContainingType.ToFullyQualifiedString(),
            method.Name,
            isAsync,
            isVoid,
            parameters,
            attributes);
    }

    private void GenerateCommandFile(SourceProductionContext sourceContext, CommandContext commandContext)
    {
        bool valid = true;
        foreach (var param in commandContext.Parameters.Where(p => p.ConversionTemplate == null))
        {
            DiagnosticDescriptor? desc = param.Classification switch
            {
                ParameterClassification.CommandName or
                ParameterClassification.Rest or
                ParameterClassification.Scalar => DiagnosticDescriptors.UnsupportedParameterType,
                _ => null
            };

            if (desc != null)
            {
                sourceContext.ReportDiagnostic(Diagnostic.Create(desc, param.Location, param.ParameterType, param.Name));
                valid = false;
            }
        }

        if (!valid)
        {
            return;
        }

        foreach (var param in commandContext.Parameters.Where(p => p.HasExplicitDefault && p.ExplicitDefaultSyntax == null))
        {
            sourceContext.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.UnsupportedParameterDefault, param.Location, param.Name));
        }

        foreach (var attribute in commandContext.Attributes)
        {
            // char.IsAsciiLetterOrDigit is a .NET 6 feature, so we have to do this manually
            // 48-57: 0-9, 65-90: A-Z, 97-122: a-z
            if (string.IsNullOrEmpty(attribute.Name) || !attribute.Name.Select(Convert.ToUInt16).All(s => (s >= 48 && s <= 57) || (s >= 65 && s <= 90) || (s >= 97 && s <= 122)))
            {
                // not a valid command name, so don't generate a thunk for it
                sourceContext.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.InvalidCommandName, attribute.Location, attribute.Name));
                continue;
            }

            string? commandResultType = commandContext.ContextType switch
            {
                ContextType.Bot => "Netwolf.BotFramework.BotCommandResult",
                _ => null
            };

            if (commandResultType == null)
            {
                sourceContext.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.InvalidCommandContext, attribute.Location));
                continue;
            }

            sourceContext.AddSource(
                $"{attribute.Name}-{commandContext.ContainerType}.{commandContext.MethodName}.g.cs",
                SourceGeneratedCommandSkeleton.GenerateThunkFile(commandContext, attribute, commandResultType));
        }
    }

    /// <summary>
    /// Gets data for a specified attribute on the symbol, or null if the attribute is not found
    /// </summary>
    /// <param name="symbol">Symbol to check</param>
    /// <param name="attributeName">Fully qualified attribute name</param>
    /// <param name="allowSubTypes">If we should allow subtypes of the attribute; this should be false if attributeName is sealed</param>
    /// <param name="includeInherited">
    /// If we should check base types of symbol; this should be true if attributeName is inherited (attributes are inherited by default).
    /// No checks are made as to whether attributeName is actually inheritable; it is assumed that it is if this value is true.
    /// </param>
    /// <returns></returns>
    private AttributeData? GetAttributeData(ISymbol? symbol, string attributeName, bool allowSubTypes = false, bool includeInherited = true)
    {
        if (symbol == null)
        {
            return null;
        }

        foreach (var attr in symbol.GetAttributes())
        {
            for (var type = attr.AttributeClass; type != null; type = type.BaseType)
            {
                if (type.ToFullyQualifiedString() == attributeName)
                {
                    return attr;
                }

                if (!allowSubTypes)
                {
                    break;
                }
            }
        }

        if (includeInherited && symbol is ITypeSymbol typeSymbol)
        {
            // AttributeUsageAttribute has Inherited = true, so we can bypass onlyInherited check for it to bring in a minor speedup
            GetAttributeData(typeSymbol.BaseType, attributeName, allowSubTypes, includeInherited);
        }

        return null;
    }

    private bool HasAttribute(ISymbol? symbol, string attributeName) => GetAttributeData(symbol, attributeName) != null;

    private bool IsOrHasInterface(ITypeSymbol type, string interfaceName)
    {
        return type.ToFullyQualifiedString() == interfaceName
            || type.AllInterfaces.Any(i => i.ToFullyQualifiedString() == interfaceName);
    }

    private ParameterClassification GetParameterClassification(IParameterSymbol param)
    {
        if (param.Type.TypeKind == TypeKind.Array)
        {
            return ParameterClassification.Array;
        }

        return param.Type.ToFullyQualifiedString() switch
        {
            "System.Threading.CancellationToken" => ParameterClassification.CancellationToken,
            _ when IsOrHasInterface(param.Type, "Netwolf.PluginFramework.Context.IContext") => ParameterClassification.IContext,
            _ when HasAttribute(param, "Netwolf.Attributes.CommandNameAttribute") => ParameterClassification.CommandName,
            _ when HasAttribute(param, "Netwolf.Attributes.RestAttribute") => ParameterClassification.Rest,
            _ => ParameterClassification.Scalar
        };
    }

    private string? GetConversionTemplate(Compilation compilation, ITypeSymbol type, string? defaultSyntax)
    {
        // is this a string? if so just use it directly and skip all of the faff below
        if (type.SpecialType == SpecialType.System_String)
        {
            return "{0}";
        }

        // is type a nullable value type? If so get conversion template for the underlying type instead
        // the compiler will lift this back to Nullable<T> in the method call as part of compiling our generated source
        if (type.SpecialType == SpecialType.System_Nullable_T)
        {
            type = (type as INamedTypeSymbol)?.TypeArguments[0] ?? type;
        }

        string typeName = type.ToFullyQualifiedString();
        defaultSyntax ??= "default";

        // built-in type handling
        string parseMethod = type.SpecialType switch
        {
            SpecialType.System_Boolean => "bool.Parse",
            SpecialType.System_Byte => "byte.Parse",
            SpecialType.System_Char => "char.Parse",
            SpecialType.System_DateTime => "DateTime.Parse",
            SpecialType.System_Decimal => "decimal.Parse",
            SpecialType.System_Double => "double.Parse",
            SpecialType.System_Enum => $"s => Enum.Parse<{typeName}>(s, true)",
            SpecialType.System_Int16 => "short.Parse",
            SpecialType.System_Int32 => "int.Parse",
            SpecialType.System_Int64 => "long.Parse",
            SpecialType.System_IntPtr => "nint.Parse",
            SpecialType.System_SByte => "sbyte.Parse",
            SpecialType.System_Single => "float.Parse",
            SpecialType.System_UInt16 => "ushort.Parse",
            SpecialType.System_UInt32 => "uint.Parse",
            SpecialType.System_UInt64 => "ulong.Parse",
            SpecialType.System_UIntPtr => "nuint.Parse",
            _ => "null"
        };

        // try to use TypeConverterAttribute
        string converter = "null";
        if (GetAttributeData(type, "System.ComponentModel.TypeConverterAttribute") is AttributeData converterData && converterData.ConstructorArguments.Length > 0)
        {
            var arg = converterData.ConstructorArguments[0];
            if (arg.Type?.SpecialType == SpecialType.System_String && arg.Value is string converterName && converterName != string.Empty)
            {
                converter = $"new {converterName}()";
            }
            else if (arg.Value is INamedTypeSymbol converterType)
            {
                converter = $"new {converterType.ToFullyQualifiedString()}()";
            }
        }

        // try explicit and implicit user-defined conversion operators from string
        // (just string, not ReadOnlySpan<char>, to maintain parity with TypeHelper.TryChangeType, since ref structs cannot be cast to dynamic)
        string castMethod = "null";
        if (compilation.ClassifyConversion(compilation.GetSpecialType(SpecialType.System_String), type).Exists)
        {
            castMethod = $"s => ({typeName})s";
        }

        // couldn't find any supported conversions, this will emit a diagnostic later on in the generator processing and avoiding generating thunks for this method
        if (parseMethod == "null" && converter == "null" && castMethod == "null")
        {
            return null;
        }

        return $"TypeHelper.ChangeType<{typeName}>({{0}}, {defaultSyntax}, {parseMethod}, {converter}, {castMethod}, out success)";
    }

    public string? GetDefaultValue(IParameterSymbol param)
    {
        // ExplicitDefaultValue will be null for structs if the explicit value is the default value of the struct
        // we'll want to remap that to default since passing null to a value type is a compilation error
        if (!param.HasExplicitDefaultValue || (param.Type.IsValueType && param.ExplicitDefaultValue == null))
        {
            return "default";
        }

        return param.ExplicitDefaultValue switch
        {
            null => "null",
            string s => SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(s)).ToString(),
            char c => SyntaxFactory.LiteralExpression(SyntaxKind.CharacterLiteralExpression, SyntaxFactory.Literal(c)).ToString(),
            bool b => b ? "true" : "false",
            // produces an expression like "(MyEnumType)123"
            _ when param.Type.SpecialType == SpecialType.System_Enum => $"({param.Type.ToFullyQualifiedString()}){param.ExplicitDefaultValue}",
            // numeric types
            byte b => b.ToString(),
            sbyte b => b.ToString(),
            short s => s.ToString(),
            ushort s => s.ToString(),
            int i => i.ToString(),
            uint i => $"{i}U",
            nint i => $"0x{i:X}",
            nuint i => $"0x{i:X}",
            long l => $"{l}L",
            ulong l => $"{l}UL",
            float f => $"{f:G9}F",
            double d => $"{d:G17}D",
            decimal d => $"{d}M",
            // something else? rather than probably causing a compile error, ignore the explicit default
            // we can emit a diagnostic for this case later because we capture the presence of an explicit default in our context records
            _ => null
        };
    }
}
