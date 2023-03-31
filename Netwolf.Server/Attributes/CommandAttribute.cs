using System.Text.RegularExpressions;

namespace Netwolf.Server.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public partial class CommandAttribute : Attribute
{
    public string Name { get; init; }

    public string? Privilege { get; init; }

    public string Parameters { get; init; }

    public CommandAttribute(string name, string parameters = "", string? privilege = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        Privilege = privilege;

        // Verify that privilege looks correct if specified
        if (privilege != null)
        {
            if (privilege.Length < 6 || privilege[5] != ':')
            {
                throw new ArgumentException($"Invalid privilege {privilege} for command {name}", nameof(privilege));
            }

            var container = privilege.AsSpan()[..4];
            switch (container)
            {
                case "user":
                case "oper":
                    break;
                case "chan":
                    // For commands that require channel privileges, a channel must be the first parameter
                    // and it must not be optional, repeated, or a list
                    if (parameters[0] != 'c' || "?*,+".Contains(parameters[1]))
                    {
                        throw new ArgumentException(
                            $"A channel privilege {privilege} was specified for command {name} but command does not begin with a mandatory channel parameter",
                            nameof(privilege));
                    }

                    break;
                default:
                    throw new ArgumentException(
                        $"Invalid privilege scope for {privilege} for command {name}, expecting user, oper, or chan",
                        nameof(privilege));
            }
        }

        // Verify that the parameters look correct
        if (!ParameterRegex().IsMatch(parameters))
        {
            throw new ArgumentException($"Invalid Parameter line {parameters} for command {name}", nameof(parameters));
        }
    }

    [GeneratedRegex(@"^(?:[snhci][?*,+]?)*(?:t\??)?$")]
    private static partial Regex ParameterRegex();
}
