using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Attributes;

/// <summary>
/// Internal attribute used by the source generator.
/// You should not manually apply this attribute to an assembly.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class SourceGeneratedCommandAttribute : Attribute
{
    public Type TargetType { get; init; }

    public Type GeneratedType { get; init; }

    public Type HandlerType { get; init; }

    public SourceGeneratedCommandAttribute(Type targetType, Type generatedType, Type handlerType)
    {
        TargetType = targetType;
        GeneratedType = generatedType;
        HandlerType = handlerType;
    }
}
