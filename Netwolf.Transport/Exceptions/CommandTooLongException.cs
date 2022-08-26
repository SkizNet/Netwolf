using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Transport.Exceptions
{
    /// <summary>
    /// Indicates an error due to a command being over the allowed protocol limits
    /// </summary>
    public class CommandTooLongException : Exception
    {
    }
}
