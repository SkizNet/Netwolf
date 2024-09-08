using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.BotFramework.Internal;

internal static class TypeHelper
{
    internal static bool TryChangeType<T>(object input, out T result)
    {
        // attempt built-in and user-defined conversions
        try
        {
            result = (T)input;
            return true;
        }
        catch (InvalidCastException) { /* try next conversion */ }
        catch (Exception)
        {
            result = default!;
            return false;
        }

        // attempt conversion through IConvertible
        try
        {
            if (input is IConvertible)
            {
                result = (T)Convert.ChangeType(input, typeof(T));
                return true;
            }
        }
        catch (Exception ex) when (ex is InvalidCastException || ex is FormatException) { /* try next conversion */ }
        catch (Exception)
        {
            result = default!;
            return false;
        }

        // attempt conversion through TypeConverterAttribute
        try
        {
            var converter = TypeDescriptor.GetConverter(typeof(T));
            if (converter.CanConvertFrom(input.GetType()))
            {
                result = (T)converter.ConvertFrom(input)!;
                return true;
            }
        }
        catch (NotSupportedException) { /* try next conversion */ }
        catch (Exception)
        {
            result = default!;
            return false;
        }

        // attempt to construct a new T via a public constructer that takes our input's type as an argument
        try
        {
            result = (T)Activator.CreateInstance(typeof(T), input)!;
            return true;
        }
        catch (Exception)
        {
            result = default!;
            return false;
        }
    }
}
