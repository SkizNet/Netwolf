using Netwolf.Server.Extensions.Internal;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Commands;

public class NumericResponse : ICommandResponse
{
    public NumericResponse(User user, Numeric numeric, params string[] args)
    {
        string? description = typeof(Numeric).GetField(numeric.ToString())!.GetCustomAttributes<DisplayAttribute>().FirstOrDefault()?.Description;
        var realArgs = new List<string?>() { user.Nickname };
        realArgs.AddRange(args);
        if (description != null)
        {
            var network = user.Network;
            realArgs.Add(description.Interpolate(user, network));
        }
    }
}
