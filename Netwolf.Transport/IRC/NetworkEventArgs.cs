namespace Netwolf.Transport.IRC;

/// <summary>
/// Arguments to events raised by <see cref="INetwork"/>.
/// </summary>
public class NetworkEventArgs : EventArgs
{
    /// <summary>
    /// Network the event was raised for. Read-only.
    /// </summary>
    public INetwork Network { get; init; }

    /// <summary>
    /// Command being processed if event is related to a command.
    /// </summary>
    public ICommand? Command { get; init; }

    /// <summary>
    /// Exception raised by the event, if any.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Cancellation token to use for any asynchronous tasks awaited by the event.
    /// </summary>
    public CancellationToken Token { get; init; }

    internal NetworkEventArgs(INetwork network, CancellationToken token, ICommand? command = null, Exception? ex = null)
    {
        Network = network;
        Command = command;
        Token = token;
        Exception = ex;
    }
}
