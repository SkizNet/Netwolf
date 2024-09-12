using Netwolf.PluginFramework.Context;

namespace Netwolf.PluginFramework.Commands;

/// <summary>
/// Service interface for a command dispatcher.
/// </summary>
/// <typeparam name="TResult"></typeparam>
public interface ICommandDispatcher<TResult>
{
    Task<TResult?> DispatchAsync(ICommand command, IContext sender, CancellationToken cancellationToken);
}
