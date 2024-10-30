using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.BotFramework.Exceptions;

/// <summary>
/// Indicates an issue with the bot's internal state.
/// For now these are treated as unrecoverable errors, however certain instances
/// may actually be recoverable by e.g. cycling channels.
/// </summary>
public class BadStateException : Exception
{
    public BadStateException() { }

    public BadStateException(string? message)
        : base(message) { }

    public BadStateException(string? message, Exception? innerException)
        : base(message, innerException) { }
}
