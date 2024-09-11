using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.PluginFramework.Commands;

/// <summary>
/// Type-erased <see cref="ICommandHandler{TResult}"/> for use in nongeneric contexts
/// </summary>
public interface ICommandHandler
{
    string Command { get; }

    string? Privilege => null;
}
