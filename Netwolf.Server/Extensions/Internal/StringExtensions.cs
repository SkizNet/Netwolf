using Netwolf.Unicode;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using static System.Net.Mime.MediaTypeNames;

namespace Netwolf.Server.Extensions.Internal;

internal static partial class StringExtensions
{
    [GeneratedRegex("{(?<exp>[^}]+)}")]
    private static partial Regex InterpolateRegex();

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

    /// <summary>
    /// Truncates a unicode string to a maximum number of bytes (in UTF-8),
    /// ensuring that we do not split in the middle of a grapheme.
    /// </summary>
    /// <param name="input">Input string</param>
    /// <param name="maxBytes">Maximum number of </param>
    /// <returns></returns>
    internal static string Truncate(this string input, int maxBytes)
    {
        var bytes = input.EncodeUtf8();
        if (bytes.Length <= maxBytes)
        {
            return input;
        }

        StringBuilder sb = new();
        int count = 0;
        var enumerator = StringInfo.GetTextElementEnumerator(input);
        while (enumerator.MoveNext())
        {
            string grapheme = enumerator.GetTextElement();
            count += grapheme.EncodeUtf8().Length;
            if (count > maxBytes)
            {
                break;
            }

            sb.Append(grapheme);
        }

        return sb.ToString();
    }
}
