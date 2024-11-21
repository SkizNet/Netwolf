// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.CSharp.RuntimeBinder;

using System.ComponentModel;
using System.Reflection;

namespace Netwolf.BotFramework.Internal;

internal static class TypeHelper
{
    private static readonly NullabilityInfoContext _context = new();

    internal static object?[] GetParameterDefaultValues(MethodInfo method)
    {
        List<object?> values = [];

        foreach (var param in method.GetParameters())
        {
            var underlyingType = Nullable.GetUnderlyingType(param.ParameterType);
            var nullability = _context.Create(param);

            if (param.HasDefaultValue && param.DefaultValue != null)
            {
                if (underlyingType != null && underlyingType.IsEnum)
                {
                    values.Add(Enum.ToObject(underlyingType, param.DefaultValue));
                }
                else
                {
                    values.Add(param.DefaultValue);
                }
            }
            else if ((!param.ParameterType.IsValueType && nullability.WriteState != NullabilityState.NotNull) || underlyingType != null)
            {
                // have a nullable (or null oblivious) reference type or nullable value type, so null is an appropriate default
                values.Add(null);
            }
            else if (param.ParameterType == typeof(string))
            {
                // non-nullable string
                values.Add(string.Empty);
            }
            else if (param.ParameterType.HasElementType)
            {
                // non-nullable array, create as empty
                var rank = param.ParameterType.GetArrayRank();
                values.Add(Array.CreateInstanceFromArrayType(param.ParameterType, new int[rank]));
            }
            else if (param.ParameterType.IsValueType || (!param.ParameterType.IsAbstract && param.ParameterType.GetConstructor(Type.EmptyTypes) != null))
            {
                // This potentially calls user-defined parameterless constructors
                values.Add(Activator.CreateInstance(param.ParameterType));
            }
            else
            {
                // it's a reference type and we have nothing better to use, so assign a default of null despite the not-null annotation
                values.Add(null);
            }
        }

        return [.. values];
    }

    // T can be either a value or reference type, so we cannot specify `out T?`
    // rather than lying to callers that result is not null when it very well might be if T is a reference type,
    // revert back to nullable oblivious context for this method
#nullable disable
    public static bool TryChangeType<T>(ReadOnlySpan<char> input, out T result)
    {
        // we need a copy here since ReadOnlySpans can't be boxed
        string inputObj = input.ToString();

        // attempt conversion through IConvertible
        try
        {
            result = (T)Convert.ChangeType(inputObj, typeof(T));
            return true;
        }
        catch (Exception ex) when (ex is InvalidCastException || ex is FormatException) { /* try next conversion */ }
        catch (Exception)
        {
            result = default;
            return false;
        }

        // attempt conversion through TypeConverterAttribute
        try
        {
            var converter = TypeDescriptor.GetConverter(typeof(T));
            result = (T)converter.ConvertFromString(inputObj);
            return true;
        }
        catch (NotSupportedException) { /* try next conversion */ }
        catch (Exception)
        {
            result = default;
            return false;
        }

        // attempt built-in and user-defined conversions
        try
        {
            // we need to thunk through dynamic in order to ensure user-defined explicit or implicit cast operators
            // are late bound; otherwise user-defined conversions can never be called because there's no way to bind
            // them to a generic type at compile time (and thunking through object fails because you can't define a
            // conversion from object and so it tries to do an explicit reference cast which fails if T isn't string)
            result = (T)(dynamic)inputObj;
            return true;
        }
        catch (Exception ex) when (ex is InvalidCastException || ex is RuntimeBinderException) { /* try next conversion */ }
        catch (Exception)
        {
            result = default;
            return false;
        }

        // attempt to construct a new T via a public constructer that takes a string as an argument
        try
        {
            result = (T)Activator.CreateInstance(typeof(T), inputObj);
            return true;
        }
        catch (Exception)
        {
            result = default;
            return false;
        }
    }
#nullable restore
}
