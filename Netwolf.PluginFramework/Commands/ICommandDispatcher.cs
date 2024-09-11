using Netwolf.PluginFramework.Context;

namespace Netwolf.PluginFramework.Commands;

/// <summary>
/// Service interface for a command dispatcher.
/// No implementations are provided in this package; each framework defines its own implementation
/// with its own implementation-defined <typeparamref name="TResult"/>.
/// </summary>
/// <typeparam name="TResult"></typeparam>
public interface ICommandDispatcher<TResult>
{
    Task<TResult?> DispatchAsync(ICommand command, IContext sender, CancellationToken cancellationToken);
}
