using Netwolf.PluginFramework.Commands;

namespace Netwolf.Transport.IRC;

/// <summary>
/// DI factory for <see cref="ICommand"/>
/// </summary>
public interface ICommandFactory
{
    Type ObjectType { get; }

    ICommand CreateCommand(CommandType commandType, string? source, string verb, IReadOnlyList<string?> args, IReadOnlyDictionary<string, string?> tags);

    /// <summary>
    /// Parse a raw IRC protocol message
    /// </summary>
    /// <param name="commandType">Whether this is a client-generated or server-generated command</param>
    /// <param name="message">Message to parse, <i>without</i> the trailing CRLF</param>
    /// <returns></returns>
    ICommand Parse(CommandType commandType, string message);
}
