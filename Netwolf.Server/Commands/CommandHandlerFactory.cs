using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Commands
{
    internal class CommandHandlerFactory : ICommandHandlerFactory
    {
        private IServiceProvider ServiceProvider { get; init; }

        private Dictionary<string, ICommandHandler> Commands { get; init; } = new();

        public CommandHandlerFactory(IServiceProvider provider, ILogger<ICommandHandlerFactory> logger)
        {
            ServiceProvider = provider;

            // populate Commands from all concrete classes across all assemblies that implement ICommandHandler
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                logger.LogTrace("Scanning {FullName} for commands", assembly.FullName);

                foreach (var type in assembly.ExportedTypes)
                {
                    if (type.IsAbstract || !type.IsAssignableTo(typeof(ICommandHandler)))
                    {
                        continue;
                    }

                    var handler = (ICommandHandler)ActivatorUtilities.CreateInstance(ServiceProvider, type);

                }
            }
        }


    }
}
