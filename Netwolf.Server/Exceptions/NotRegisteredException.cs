using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Exceptions;

public class NotRegisteredException : CommandException
{
    public NotRegisteredException()
        : base(Numeric.ERR_NOTREGISTERED) { }
}
