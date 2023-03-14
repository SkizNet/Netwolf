using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.Serialization;

using Newtonsoft.Json.Linq;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Netwolf.Test
{
    internal static class Extensions
    {
        internal static string Interpolate<T1>(
            this string input,
            T1 param1,
            [CallerArgumentExpression(nameof(param1))] string? param1Name = null)
        {
            if (string.IsNullOrWhiteSpace(param1Name))
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
            if (string.IsNullOrWhiteSpace(param1Name) || string.IsNullOrWhiteSpace(param2Name))
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
            return Regex.Replace(input, @"{(?<exp>[^}]+)}", match => {
                var e = DynamicExpressionParser.ParseLambda(parameters, null, match.Groups["exp"].Value);
                return e.Compile().DynamicInvoke(args)?.ToString() ?? string.Empty;
            });
        }
    }
}
