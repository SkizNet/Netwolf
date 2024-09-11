using Netwolf.PluginFramework.Context;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.PluginFramework.Commands;

public class CommandValidator<TResult> : ICommandValidator<TResult>
{
    public void ValidateCommand(ICommand command, ICommandHandler<TResult> commandHandler, IContext context) { }

    public bool ValidateCommandHandler(ICommandHandler<TResult> commandHandler)
    {
        return true;
    }

    public bool ValidateCommandType(Type commandType)
    {
        return true;
    }
}
