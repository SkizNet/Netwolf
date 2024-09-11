using Netwolf.PluginFramework.Commands;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.PluginFramework.Context;

/// <summary>
/// Pass contexts through unchanged, for when no other context augmenters are registered
/// in the DI container
/// </summary>
internal class DummyContextAugmenter : IContextAugmenter
{
    public IContext AugmentForCommand(IContext context, ICommand command, ICommandHandler handler)
    {
        return context;
    }
}
