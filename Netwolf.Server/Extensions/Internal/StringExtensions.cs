using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Netwolf.Server.Extensions.Internal;

internal static partial class StringExtensions
{
    internal static string Interpolate<T1>(
        this string input,
        T1 param1,
        [CallerArgumentExpression(nameof(param1))] string? param1Name = null)
    {
        if (String.IsNullOrWhiteSpace(param1Name))
        {
            throw new InvalidOperationException("Argument passed to Interpolate cannot be resolved into a parameter name");
        }
        
        return InterpolateReal(input, new[] { Expression.Parameter(typeof(T1), param1Name) }, param1);
    }

    internal static string Interpolate<T1, T2>(
        this string input,
        T1 param1,
        T2 param2,
        [CallerArgumentExpression(nameof(param1))] string? param1Name = null,
        [CallerArgumentExpression(nameof(param2))] string? param2Name = null)
    {
        if (String.IsNullOrWhiteSpace(param1Name) || String.IsNullOrWhiteSpace(param2Name))
        {
            throw new InvalidOperationException("Argument passed to Interpolate cannot be resolved into a parameter name");
        }

        return InterpolateReal(
            input,
            new[] { Expression.Parameter(typeof(T1), param1Name), Expression.Parameter(typeof(T2), param2Name) },
            param1,
            param2);
    }

    private static string InterpolateReal(string input, ParameterExpression[] parameters, params object?[] args)
    {
        return InterpolateRegex().Replace(input, match =>
        {
            var e = DynamicExpressionParser.ParseLambda(parameters, null, match.Groups["exp"].Value);
            return e.Compile().DynamicInvoke(args)?.ToString() ?? String.Empty;
        });
    }

    [GeneratedRegex("{(?<exp>[^}]+)}")]
    private static partial Regex InterpolateRegex();
}
