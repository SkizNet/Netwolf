using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.PluginFramework.Context;

/// <summary>
/// Defines a "context" (e.g. user, server, or channel) and
/// holds data about that context. switch expressions or
/// is expressions can be used to downcast this to the appropriate
/// underlying implementation type depending on the framework in use.
/// </summary>
public interface IContext
{
}
