using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Commands;

public class CommandResponse : ICommandResponse
{
    public bool CloseConnection => false;

    public CommandResponse(User user, string command, params string[] args)
    {

    }
}
