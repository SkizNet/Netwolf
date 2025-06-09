using Netwolf.Transport.IRC;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Test.PluginFramework;

public class Validators
{
    public class AllowOnlyB<T> : ICommandValidator<T>
    {
        public void ValidateCommand(ICommand command, ICommandHandler<T> commandHandler, IContext context) { }
        public bool ValidateCommandHandler(ICommandHandler<T> commandHandler) => commandHandler.GetType() == typeof(Commands.TestB);
        public bool ValidateCommandType(Type commandType) => commandType.Name == "TestA" || commandType.Name == "TestB";
    }
}
