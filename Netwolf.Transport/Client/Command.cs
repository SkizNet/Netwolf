using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Transport.Client
{
    /// <summary>
    /// Represents a command that can be sent to or received from a network
    /// </summary>
    public class Command : ICommand
    {
        public CommandType CommandType { get; init; }

        public string? Source { get; init; }

        public string Verb { get; init; }

        public IReadOnlyList<string> Args { get; init; }

        public IReadOnlyDictionary<string, string?> Tags { get; init; }

        public bool HasTrailingArg { get; init; }

        public INetwork Network { get; init; }

        public Command(INetwork network, CommandOptions options)
        {
            CommandType = options.CommandType;
            Source = options.Source;
            Verb = options.Verb;
            Args = options.Args;
            Tags = options.Tags;
            HasTrailingArg = options.HasTrailingArg;
            Network = network;
        }
    }
}
