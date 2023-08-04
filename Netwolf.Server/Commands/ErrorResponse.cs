using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Commands;

/// <summary>
/// Indicates a fatal error that should close the client connection
/// </summary>
public class ErrorResponse : ICommandResponse
{
    public bool CloseConnection => true;

    public ErrorResponse(User user, string message)
    {

    }
}
