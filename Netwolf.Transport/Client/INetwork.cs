using Netwolf.Transport.Exceptions;

namespace Netwolf.Transport.Client
{
    /// <summary>
    /// Represents a Network
    /// </summary>
    public interface INetwork : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// Network name
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// True if we are currently connected to this Network
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Connect to the network and perform user registration. If the passed-in
        /// cancellation token has a timeout, that timeout will apply to all connection
        /// attempts, rather than any individual connection. Individual connection
        /// timeouts are controlled by the <see cref="NetworkOptions"/> passed in
        /// while creating the <see cref="INetwork"/>.
        /// </summary>
        /// <param name="cancellationToken">
        /// Cancellation token; passing <see cref="CancellationToken.None"/>
        /// will retry connections indefinitely until the connection happens.
        /// </param>
        Task ConnectAsync(CancellationToken cancellationToken);

        ICommand[] PrepareMessage(MessageType messageType, string target, string text, IReadOnlyDictionary<string, object?>? tags);

        /// <summary>
        /// Prepare a command to be sent to the network.
        /// </summary>
        /// <param name="verb">Command to send.</param>
        /// <param name="args">
        /// Command arguments, which will be turned into strings.
        /// </param>
        /// <param name="tags">
        /// Command tags. <c>null</c> values will be sent without tag values, whereas all other values
        /// will be turned into strings. If the resultant value is an empty string, it will be sent without a tag value.
        /// </param>
        /// <returns>The prepared command, which can be sent to the network via <see cref="SendAsync(ICommand)"/>.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="verb"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">If <paramref name="verb"/> is invalid.</exception>
        /// <exception cref="ArgumentException">If a member of <paramref name="args"/> except for the final member would be considered a trailing argument.</exception>
        /// <exception cref="CommandTooLongException">
        /// If the expanded command (without tags) cannot fit within 512 bytes or the tags cannot fit within 4096 bytes.
        /// </exception>
        ICommand PrepareCommand(string verb, object[]? args, IReadOnlyDictionary<string, object?>? tags);

        Task SendAsync(ICommand command);

        /// <summary>
        /// Cleanly disconnect from the network with the default timeout (5 seconds).
        /// If the connection isn't cleanly closed in time, it will be forcibly closed instead.
        /// </summary>
        /// <param name="reason">Reason used in the QUIT message, displayed to others on the network</param>
        /// <returns></returns>
        Task DisconnectAsync(string reason);

        /// <summary>
        /// Cleanly disconnect from the network with a user-controlled cancellation policy.
        /// If the connection cannot be cleanly closed in time, it will be forcibly closed instead.
        /// </summary>
        /// <param name="reason">Reason used in the QUIT message, displayed to others on the network</param>
        /// <param name="cancellationToken">
        /// Cancellation token; passing <see cref="CancellationToken.None"/>
        /// will block indefinitely until the connection is cleanly closed by the other end.
        /// </param>
        /// <returns></returns>
        Task DisconnectAsync(string reason, CancellationToken cancellationToken);
    }
}
