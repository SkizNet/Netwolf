using System.Text;

namespace Netwolf.Transport.Client;

/// <summary>
/// General client interface for a <see cref="Command"/>.
/// </summary>
public interface ICommand
{
    /// <summary>
    /// Type of command this is
    /// </summary>
    CommandType CommandType { get; }

    /// <summary>
    /// The sender of this command, may be <c>null</c>.
    /// </summary>
    string? Source { get; }

    /// <summary>
    /// Command verb, normalized to all-uppercase.
    /// </summary>
    string Verb { get; }

    /// <summary>
    /// Command arguments, normalized such that all arguments except for the final
    /// argument lack spaces, are not empty strings, and do not begin with colons,
    /// and the final argument may have spaces, may be an empty string, or may begin with a colon.
    /// </summary>
    IReadOnlyList<string> Args { get; }

    /// <summary>
    /// Tags to be sent to the remote network as part of this command.
    /// A <c>null</c> value indicates that the tag will be sent without a value.
    /// </summary>
    IReadOnlyDictionary<string, string?> Tags { get; }

    /// <summary>
    /// If <c>true</c>, the final argument in <see cref="Args"/> will have a colon prefixed to it
    /// in the final command
    /// </summary>
    bool HasTrailingArg { get; }

    /// <summary>
    /// The tag part of the final command, including the trailing space if tags exist,
    /// in the format accepted by the remote IRC server
    /// </summary>
    string TagPart
    {
        get
        {
            if (Tags.Count == 0)
            {
                return String.Empty;
            }

            var sb = new StringBuilder();
            bool initial = true;
            _ = sb.Append('@');

            foreach (var (key, value) in Tags)
            {
                if (initial)
                {
                    initial = false;
                }
                else
                {
                    _ = sb.Append(';');
                }

                _ = sb.Append(key);

                if (value == null)
                {
                    continue;
                }

                _ = sb.Append('=');

                foreach (char c in value)
                {
                    _ = sb.Append(c switch
                    {
                        ';' => @"\:",
                        ' ' => @"\s",
                        '\\' => @"\\",
                        '\r' => @"\r",
                        '\n' => @"\n",
                        _ => c
                    });
                }
            }

            _ = sb.Append(' ');
            return sb.ToString();
        }
    }

    /// <summary>
    /// The command part of the final command, including trailing CRLF
    /// and the source prefix.
    /// </summary>
    string PrefixedCommandPart => $":{Source} {UnprefixedCommandPart}";

    /// <summary>
    /// The command part of the final command, including trailing CRLF.
    /// This does not include the source prefix.
    /// </summary>
    string UnprefixedCommandPart
    {
        get
        {
            var sb = new StringBuilder();
            _ = sb.Append(Verb);

            for (int i = 0; i < Args.Count; ++i)
            {
                _ = sb.Append(' ');

                if (i == Args.Count - 1 && HasTrailingArg)
                {
                    _ = sb.Append(':');
                }

                _ = sb.Append(Args[i]);
            }

            _ = sb.Append("\r\n");
            return sb.ToString();
        }
    }

    /// <summary>
    /// The full command to send to the remote IRC server, without trailing CRLF.
    /// </summary>
    string FullCommand => (Source == null || CommandType == CommandType.Client)
        ? $"{TagPart}{UnprefixedCommandPart}"
        : $"{TagPart}{PrefixedCommandPart}";
}
