using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Commands;

/// <summary>
/// Indicates a fatal error that should close the client connection
/// </summary>
public class ErrorResponse : CommandResponse
{
    public override bool CloseConnection => true;

    public ErrorResponse(User user, string message)
        : base(user, null, "ERROR", message)
    {

    }
}
