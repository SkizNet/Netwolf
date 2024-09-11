using Netwolf.Server.Commands;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Exceptions;

public class CommandException : Exception
{
    public Numeric Numeric { get; init; }

    public ImmutableArray<string> Args { get; init; }

    public CommandException(Numeric numeric, params string[] args)
    {
        Numeric = numeric;
        Args = [.. args];
    }

    public NumericResponse GetNumericResponse(User user)
    {
        return new NumericResponse(user, Numeric, [.. Args]);
    }
}
