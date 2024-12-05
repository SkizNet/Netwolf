// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.CSharp.RuntimeBinder;

using System.ComponentModel;
using System.Reflection;

namespace Netwolf.PluginFramework.Util;

public static class TypeHelper
{
    private static readonly NullabilityInfoContext _context = new();

    public static object?[] GetParameterDefaultValues(MethodInfo method)
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
    // revert back to nullable oblivious context for these methods
#nullable disable
    /// <summary>
    /// Attempt to convert a string to a specific type via Reflection.
    /// The following are tried in-order, moving on to the next if the previous fails:
    /// 1. Builtin conversions (IConvertible and Enum parsing)
    /// 2. TypeConverterAttribute
    /// 3. User-defined explicit or implicit conversion operators
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="input"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    public static bool TryChangeType<T>(string input, out T result)
    {
        // If T is a nullable value type, we need to convert to the underlying type instead
        Type convertTo = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

        if (input == null && (!typeof(T).IsValueType || convertTo != typeof(T)))
        {
            result = default;
            return true;
        }

        // attempt conversion through IConvertible
        try
        {
            result = (T)Convert.ChangeType(input, convertTo);
            return true;
        }
        catch (Exception ex) when (ex is InvalidCastException || ex is FormatException) { /* try next conversion */ }
        catch (Exception)
        {
            result = default;
            return false;
        }

        // attempt conversion to an enumeration
        if (convertTo.IsEnum)
        {
            try
            {
                if (Enum.TryParse(convertTo, input, true, out var parsed))
                {
                    result = (T)parsed;
                    return true;
                }
            }
            catch (Exception)
            {
                result = default;
                return false;
            }
        }

        // attempt conversion through TypeConverterAttribute
        // if T is nullable we want to call that specific converter
        try
        {
            var converter = TypeDescriptor.GetConverter(typeof(T));
            result = (T)converter.ConvertFromString(input);
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
            result = (T)(dynamic)input;
            return true;
        }
        catch (Exception ex) when (ex is InvalidCastException || ex is RuntimeBinderException) { /* try next conversion */ }
        catch (Exception)
        {
            result = default;
            return false;
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Attempt to convert a string to a specific type via predetermined methods.
    /// This overload is called via the source generator and does not require Reflection, unlike the other TryChangeType overload.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="input"></param>
    /// <param name="defaultValue">Value to use if conversions fail</param>
    /// <param name="parseMethod">The builtin whatever.Parse method group or a lambda wrapping Enum.Parse</param>
    /// <param name="converter">A TypeConverter retrieved from TypeConverterAttribute</param>
    /// <param name="castMethod">A lambda wrapping a typecast expression</param>
    /// <param name="success">Whether we successfully converted input or if we used defaultValue</param>
    /// <returns></returns>
    public static T ChangeType<T>(string input, T defaultValue, Func<string, T> parseMethod, TypeConverter converter, Func<string, T> castMethod, out bool success)
    {
        success = true;
        if (input == null && (!typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) != null))
        {
            return default;
        }

        if (parseMethod != null)
        {
            try
            {
                return parseMethod(input);
            }
            catch (Exception ex) when (ex is InvalidCastException || ex is FormatException) { /* try next conversion */ }
            catch (Exception)
            {
                success = false;
                return defaultValue;
            }
        }

        if (converter != null)
        {
            try
            {
                return (T)converter.ConvertFromString(input);
            }
            catch (NotSupportedException) { /* try next conversion */ }
            catch (Exception)
            {
                success = false;
                return defaultValue;
            }
        }

        if (castMethod != null)
        {
            try
            {
                return castMethod(input);
            }
            catch (InvalidCastException) { /* try next conversion */ }
            catch (Exception)
            {
                success = false;
                return defaultValue;
            }
        }

        success = false;
        return defaultValue;
    }
#nullable restore
}
