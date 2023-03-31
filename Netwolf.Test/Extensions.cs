using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Netwolf.Test;

internal static class Extensions
{
    internal static string Interpolate<T1>(
        this string input,
        T1 param1,
        [CallerArgumentExpression(nameof(param1))] string? param1Name = null)
    {
        return String.IsNullOrWhiteSpace(param1Name)
            ? throw new InvalidOperationException("Argument passed to Interpolate cannot be resolved into a parameter name")
            : InterpolateReal(input, new[] { Expression.Parameter(typeof(T1), param1Name) }, param1);
    }

    internal static string Interpolate<T1, T2>(
        this string input,
        T1 param1,
        T2 param2,
        [CallerArgumentExpression(nameof(param1))] string? param1Name = null,
        [CallerArgumentExpression(nameof(param2))] string? param2Name = null)
    {
        return String.IsNullOrWhiteSpace(param1Name) || String.IsNullOrWhiteSpace(param2Name)
            ? throw new InvalidOperationException("Argument passed to Interpolate cannot be resolved into a parameter name")
            : InterpolateReal(
            input,
            new[] { Expression.Parameter(typeof(T1), param1Name), Expression.Parameter(typeof(T2), param2Name) },
            param1,
            param2);
    }

    private static string InterpolateReal(string input, ParameterExpression[] parameters, params object?[] args)
    {
        return Regex.Replace(input, @"{(?<exp>[^}]+)}", match =>
        {
            var e = DynamicExpressionParser.ParseLambda(parameters, null, match.Groups["exp"].Value);
            return e.Compile().DynamicInvoke(args)?.ToString() ?? String.Empty;
        });
    }
}
