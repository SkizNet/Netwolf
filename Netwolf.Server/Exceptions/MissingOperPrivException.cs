using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Exceptions;

public class MissingOperPrivException : CommandException
{
    public MissingOperPrivException(string privilege)
        : base(Numeric.ERR_NOPRIVS, privilege) { }
}
