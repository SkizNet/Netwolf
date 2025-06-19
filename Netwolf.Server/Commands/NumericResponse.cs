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
        // Nickname might be null if we need to send a numeric before the user sends a valid NICK message
        var realArgs = new List<string>() { user.Nickname ?? "*" };
        realArgs.AddRange(args);
        if (description != null)
        {
            var network = user.Network;
            realArgs.Add(description.Interpolate(user, network));
        }

        Args.AddRange(realArgs);
    }

    /// <summary>
    /// Numeric response with a custom description (as opposed to a description defined as a resource)
    /// </summary>
    /// <param name="user"></param>
    /// <param name="numeric"></param>
    /// <param name="description">Custom description, or null to omit the description</param>
    /// <param name="args"></param>
    public NumericResponse(User user, Numeric numeric, string? description, IEnumerable<string> args)
        : base(user, null, ((int)numeric).ToString("D3"))
    {
        // Nickname might be null if we need to send a numeric before the user sends a valid NICK message
        var realArgs = new List<string>() { user.Nickname ?? "*" };
        realArgs.AddRange(args);
        if (description != null)
        {
            var network = user.Network;
            realArgs.Add(description.Interpolate(user, network));
        }

        Args.AddRange(realArgs);
    }
}
