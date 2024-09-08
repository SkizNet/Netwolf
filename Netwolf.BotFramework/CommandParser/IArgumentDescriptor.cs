using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.BotFramework.CommandParser;

internal interface IArgumentDescriptor
{
    bool TryParse(ref ReadOnlySpan<string> args, out object? result);
}
