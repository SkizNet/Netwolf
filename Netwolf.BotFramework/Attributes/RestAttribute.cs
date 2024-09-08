using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.BotFramework.Attributes;

/// <summary>
/// Collects the remainder of the arguments into this parameter.
/// The parameter type must be string or have an explicit conversion from string.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public class RestAttribute : Attribute
{
}
