using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.BotFramework.Attributes;

/// <summary>
/// Indicates that multiple spaces in a row should be preserved for this parameter
/// while it is being converted to the target type. Trailing spaces will still be
/// omitted unless the parameter is also decorated with <see cref="RestAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public class RawAttribute : Attribute
{
}
