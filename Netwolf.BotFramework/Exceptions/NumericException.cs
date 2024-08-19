using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.BotFramework.Exceptions;

/// <summary>
/// Represents an operation that failed with an error Numeric code
/// </summary>
public class NumericException : Exception
{
    public int Numeric { get; init; }

    public NumericException(int numeric, string message) : base(message)
    {
        Numeric = numeric;
    }
}
