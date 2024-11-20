// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.CSharp.RuntimeBinder;

using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Netwolf.BotFramework.Internal;

internal static class TypeHelper
{
    internal static object?[] GetParameterDefaultValues(MethodInfo method)
    {
        List<object?> values = [];

        foreach (var param in method.GetParameters())
        {
            var underlyingType = Nullable.GetUnderlyingType(param.ParameterType);

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
            else if (!param.ParameterType.IsValueType || underlyingType != null)
            {
                // have a reference type or nullable value type, so null is an appropriate default
                values.Add(null);
            }
            else
            {
                // value type without default; these are always considered initialized
                // so GetUninitializedObject returns the default value without calling user-defined
                // parameterless constructors
                values.Add(RuntimeHelpers.GetUninitializedObject(param.ParameterType));
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
#nullable enable
}
