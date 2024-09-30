using Netwolf.BotFramework.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.BotFramework.Permissions;

internal class OperTagPermissionProvider : IPermissionProvider, ICapProvider
{
    private record CapAndTag(string Cap, string Tag);

    // in order of preference (most preferred to least)
    // all caps listed here will be enabled if possible due to the API not supporting other means of operation yet;
    // can revisit in the event that we start running into limits on the tag part of the message
    private readonly CapAndTag[] _tags = [
        new("solanum.chat/oper", "solanum.chat/oper")
        ];

    // to avoid extra allocations
    private readonly string[] _operPermission = ["oper"];

    public Task<IEnumerable<string>> GetPermissionsAsync(BotCommandContext context, CancellationToken cancellationToken)
    {
        foreach (var (cap, tag) in _tags)
        {
            if (context.NetworkInfo.TryGetEnabledCap(cap, out _) && context.Command.Tags.TryGetValue(tag, out _))
            {
                return Task.FromResult((IEnumerable<string>)_operPermission);
            }
        }

        return Task.FromResult((IEnumerable<string>)[]);
    }

    public bool ShouldEnable(string cap, string? value)
    {
        return _tags.Any(o => o.Cap == cap);
    }
}
