using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Transport.Client
{
    /// <summary>
    /// DI factory for <see cref="ICommand"/>
    /// </summary>
    public interface ICommandFactory
    {
        Type CommandType { get; }

        ICommand CreateCommand(CommandType commandType, string? source, string verb, List<string> args, Dictionary<string, string?> tags);
    }
}
