using Netwolf.Server.Extensions.Internal;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Commands;

public class NumericResponse : CommandResponse
{
    public NumericResponse(User user, Numeric numeric, params string[] args)
        : base(user, null, ((int)numeric).ToString("D3"))
    {
        string? description = typeof(Numeric).GetField(numeric.ToString())!.GetCustomAttributes<DisplayAttribute>().FirstOrDefault()?.GetDescription();
        var realArgs = new List<string>() { user.Nickname };
        realArgs.AddRange(args);
        if (description != null)
        {
            var network = user.Network;
            realArgs.Add(description.Interpolate(user, network));
        }

        Args.AddRange(realArgs);
    }
}
