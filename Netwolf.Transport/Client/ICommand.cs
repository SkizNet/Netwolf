using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Transport.Client
{
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
        /// The sender of this command. May be filled in for commands received from the server,
        /// but will be <c>null</c> for commands prepared by the client.
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
    }
}
