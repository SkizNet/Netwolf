using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Commands;

public class EmptyResponse : ICommandResponse
{
    public bool CloseConnection => false;

    public void Send()
    {
        // no-op
    }
}
