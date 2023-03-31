namespace Netwolf.Transport.Client;

/// <summary>
/// Encapsulates data required to create an <see cref="ICommand"/>.
/// </summary>
/// <param name="CommandType">Type of command (client or server)</param>
/// <param name="Source">Command source, <c>null</c> for client commands and potentially <c>null</c> for server commands if the server did not specify a source</param>
/// <param name="Verb">Command verb (name), normalized to all-uppercase</param>
/// <param name="Args">Command arguments</param>
/// <param name="Tags">Command tags; <c>null</c> values indicate value-less tags</param>
/// <param name="HasTrailingArg">Will be <c>true</c> if the last element of <paramref name="Args"/> requires a colon prefix</param>
public record CommandOptions(CommandType CommandType, string? Source, string Verb, List<string> Args, Dictionary<string, string?> Tags, bool HasTrailingArg);
