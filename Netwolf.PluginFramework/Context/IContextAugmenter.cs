using Netwolf.PluginFramework.Commands;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.PluginFramework.Context;

/// <summary>
/// Services to augment an <see cref="IContext"/> with additional data.
/// These are called in series as a pipeline; augmenters should examine the context
/// type being passed in, and if it is a supported type, add whichever data they desire to it.
/// They should then return the resulting context (or the original context if it is an unsupported type).
/// </summary>
public interface IContextAugmenter
{
    IContext AugmentForCommand(IContext context, ICommand command, ICommandHandler handler);
}
