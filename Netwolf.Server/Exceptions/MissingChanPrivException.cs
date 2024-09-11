using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Exceptions;

public class MissingChanPrivException : CommandException
{
    public MissingChanPrivException(string channel)
        : base(Numeric.ERR_CHANOPPRIVSNEEDED, channel) { }
}
