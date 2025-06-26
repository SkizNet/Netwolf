using Netwolf.Server.Commands;
using Netwolf.Transport.Commands;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Sasl;

public interface ISaslState
{
    string Name { get; }

    bool Completed { get; }

    bool Errored { get; }

    Task<ICommandResponse> ProcessClientCommandAsync(ICommand command, CancellationToken token);
}
