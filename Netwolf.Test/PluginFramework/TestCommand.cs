using Netwolf.PluginFramework.Commands;
using Netwolf.Transport.Commands;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Test.PluginFramework;
internal class TestCommand : ICommand
{
    public CommandType CommandType => CommandType.Bot;
    public string? Source => null;
    public string Verb { get; init; }
    public ImmutableList<string> Args => [];
    public ImmutableDictionary<string, string?> Tags => ImmutableDictionary<string, string?>.Empty;
    public bool HasTrailingArg => false;

    internal TestCommand(string verb)
    {
        Verb = verb;
    }
}
