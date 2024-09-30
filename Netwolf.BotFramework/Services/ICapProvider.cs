using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.BotFramework.Services;

/// <summary>
/// Service that designates that a particular ircd capability (CAP) is supported
/// </summary>
public interface ICapProvider
{
    /// <summary>
    /// Whether or not the particular capability should be enabled.
    /// When multiple providers are registered, a cap will be enabled if at least one of them returns true from this method.
    /// </summary>
    /// <param name="cap">Capability name</param>
    /// <param name="value">Capability value (null if the ircd did not specify a value)</param>
    /// <returns>True if the cap should be enabled, false if this provider doesn't support this cap</returns>
    bool ShouldEnable(string cap, string? value);
}
