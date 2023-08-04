using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Commands;

public interface ICommandResponse
{
    /// <summary>
    /// Whether we should terminate the client connection as part of this response (error, quit)
    /// </summary>
    bool CloseConnection { get; }
}
