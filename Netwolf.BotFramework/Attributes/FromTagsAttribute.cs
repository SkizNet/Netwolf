using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.BotFramework.Attributes;

/// <summary>
/// Indicates that this parameter should be resolved using message tags rather than message body
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class FromTagsAttribute : Attribute
{

}
