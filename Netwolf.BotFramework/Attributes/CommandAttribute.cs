using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Netwolf.BotFramework.Attributes;

/// <summary>
/// Attribute to denote a user-defined bot command. These can be addressed to the bot
/// directly or sent in channel prefixed by the command character.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed partial class CommandAttribute : Attribute
{
    /// <summary>
    /// Command name. Names must contain only ascii letters and numbers and cannot begin with a number.
    /// If null, the method name is used provided it meets criteria for a valid name
    /// (otherwise an exception is thrown).
    /// </summary>
    public string? Name { get; private set; }

    /// <summary>
    /// In which contexts should the command be accepted as valid?
    /// </summary>
    public CommandTargeting Targeting { get; init; } = CommandTargeting.AllowAll;

    // TODO: Add an assembly-scoped attribute to specify a default ResourceType for all localized lookups
    /// <summary>
    /// If set, <see cref="Name"/>, <see cref="Summary"/>, and <see cref="HelpText"/>
    /// will be treated as names of public static properties of this type to fetch localized text.
    /// </summary>
    public Type? ResourceType { get; init; }

    /// <summary>
    /// Short summary description of what the command does, for use in command listings.
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// Long description of what the command does, for when displaying help for a specific command.
    /// May be multiple lines; overly long descriptions will be paged. Text wrapping will be employed
    /// if a particular line is too long.
    /// </summary>
    public string? HelpText { get; init; }

    [GeneratedRegex("^[a-z][a-z0-9]*$")]
    private static partial Regex ValidNameRegex();

    /// <summary>
    /// Defines a callback for a bot command
    /// </summary>
    /// <param name="name">Command name, or <c>null</c> to inherit the name of the method or class. Coerced to lowercase. Must consist only of ascii letters and numbers, and cannot begin with a number.</param>
    /// <param name="category">Command name, or <c>null</c> to inherit the category from a CommandCategoryAttribute or the default of <c>"command"</c>. Coerced to lowercase. Must consist only of ascii letters and numbers, and cannot begin with a number.</param>
    /// /// <exception cref="ArgumentException">Thrown when the chosen name or category is not null but invalid</exception>
    public CommandAttribute(string? name = null)
    {
        if (name != null)
        {
            // Name is always null at this point, but this call lets us avoid some code duplication
            SetNameIfNull(name);
        }
    }

    internal void SetNameIfNull(string name)
    {
        if (Name != null)
        {
            return;
        }

        var lower = name.ToLowerInvariant();
        if (!ValidNameRegex().IsMatch(lower))
        {
            throw new ArgumentException("Names may consist only of ascii letters and numbers, and cannot begin with a number.", nameof(name));
        }

        Name = lower;
    }
}
