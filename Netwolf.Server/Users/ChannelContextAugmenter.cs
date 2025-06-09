using Netwolf.PluginFramework.Commands;
using Netwolf.PluginFramework.Context;
using Netwolf.Server.Commands;

namespace Netwolf.Server.Users;

internal class ChannelContextAugmenter : IContextAugmenter
{
    public void AugmentForCommand(IContext context, ICommand command, ICommandHandler handler)
    {
        if (context is not ServerContext serverContext || handler is not ServerCommandHandler commandHandler)
        {
            return;
        }

        if (commandHandler.HasChannel && serverContext.User != null)
        {
            // command.Args[0] should represent a single channel name
            if (serverContext.User.Network.Channels.TryGetValue(command.Args[0], out var channel))
            {
                serverContext.Channel = channel;
            }
        }
    }
}
