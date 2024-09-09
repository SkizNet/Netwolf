using System.Collections.Immutable;

namespace Netwolf.Transport.IRC;

/// <summary>
/// Represents a command that can be sent to or received from a network
/// </summary>
public class Command : ICommand
{
    public CommandType CommandType { get; init; }

    public string? Source { get; init; }

    public string Verb { get; init; }

    public ImmutableList<string> Args { get; init; }

    public ImmutableDictionary<string, string?> Tags { get; init; }

    public bool HasTrailingArg { get; init; }

    public Command(CommandOptions options)
    {
        CommandType = options.CommandType;
        Source = options.Source;
        Verb = options.Verb;
        Args = [.. options.Args];
        Tags = options.Tags.ToImmutableDictionary();
        HasTrailingArg = options.HasTrailingArg;
    }
}
