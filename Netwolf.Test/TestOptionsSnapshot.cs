using Microsoft.Extensions.Options;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Test;

internal class TestOptionsSnapshot<TOptions> : IOptionsSnapshot<TOptions>
    where TOptions : class
{
    public required TOptions Value { get; set; }

    public TOptions Get(string? name) => Value;
}
