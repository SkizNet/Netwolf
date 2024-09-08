using Netwolf.BotFramework.Attributes;
using Netwolf.BotFramework.Internal;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.Design;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.BotFramework.CommandParser;

internal class ArgumentDescriptor<TResult> : IArgumentDescriptor
{
    private string Name { get; init; }

    private List<ValidationAttribute> Validators { get; init; }

    private bool IsRaw { get; init; }

    private bool IsRest { get; init; }

    private bool IsRequired { get; init; }

    public ArgumentDescriptor(ParameterInfo info)
    {
        var context = new NullabilityInfoContext();

        Name = info.Name ?? throw new ArgumentException("Bot command methods must not have unnamed parameters");
        Validators = info.GetCustomAttributes<ValidationAttribute>().ToList();
        IsRaw = info.GetCustomAttribute<RawAttribute>() != null;
        IsRest = info.GetCustomAttribute<RestAttribute>() != null;

        // if we have [Required] or are not nullable, we treat this as required
        // NullabilityInfoContext looks at both nullable reference as well as nullable value types
        IsRequired =
            info.GetCustomAttribute<RequiredAttribute>() != null
            || context.Create(info).WriteState == NullabilityState.NotNull;
    }

    /// <summary>
    /// Attempt to extract a <typeparamref name="TResult"/> from <paramref name="args"/>.
    /// Side effect: removes elements from the beginning of <paramref name="args"/> that
    /// were consumed in the parsing of <paramref name="result"/>.
    /// </summary>
    /// <param name="args"></param>
    /// <param name="result"></param>
    /// <returns>
    /// Whether or not we successfully obtained a <paramref name="result"/>.
    /// This will only return <c>false</c> if the argument is optional, otherwise it will
    /// throw an exception upon error.
    /// </returns>
    public bool TryParse(ref ReadOnlySpan<string> args, out TResult? result)
    {
        if (!IsRaw)
        {
            while (args[0] == string.Empty)
            {
                args = args[1..];
            }
        }

        string value = IsRest ? string.Join(' ', args.ToArray()) : args[0];
        var success = TypeHelper.TryChangeType<TResult>(value, out var converted);
        result = converted;

        if (!success)
        {
            if (!IsRequired)
            {
                return false;
            }

            // TODO: figure out how to get a localized message here
            throw new ValidationException($"Unable to convert {Name} to {typeof(TResult).Name}");
        }

        try
        {
            Validators.ForEach(v => v.Validate(converted, Name));
        }
        catch (ValidationException)
        {
            if (!IsRequired)
            {
                // if validation failed and we're optional, skip us over
                return false;
            }

            throw;
        }

        args = IsRest ? [] : args[1..];
        return true;
    }

    bool IArgumentDescriptor.TryParse(ref ReadOnlySpan<string> args, out object? result)
    {
        var success = TryParse(ref args, out var obj);
        result = obj;
        return success;
    }
}
