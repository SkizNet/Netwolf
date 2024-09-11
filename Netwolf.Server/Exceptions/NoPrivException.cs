using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Exceptions;

public class NoPrivException : CommandException
{
    public NoPrivException()
        : base(Numeric.ERR_NOPRIVILEGES) { }
}
