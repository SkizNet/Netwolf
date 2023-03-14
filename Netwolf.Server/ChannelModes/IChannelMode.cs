namespace Netwolf.Server.ChannelModes
{
    /// <summary>
    /// Internal API surface for channel modes.
    /// </summary>
    internal interface IChannelMode
    {
        /// <summary>
        /// Singleton instance of this channel mode.
        /// </summary>
        IChannelMode Singleton { get; }

        /// <summary>
        /// Character representing this mode in a mode line.
        /// </summary>
        char ModeChar { get; }

        /// <summary>
        /// How this mode handles parameters.
        /// </summary>
        ParameterType ParameterType { get; }

        /// <summary>
        /// Privilege required in order to view whether or not
        /// this mode is set in the channel. If <c>null</c>,
        /// anyone can view the mode is set.
        /// </summary>
        string? ViewModePrivilege { get; }

        /// <summary>
        /// Privilege required in order to view the parameter
        /// for this mode, or the list for list modes. If <c>null</c>,
        /// anyone can view the parameter/list so long as they also
        /// have permission to view the mode itself.
        /// </summary>
        string? ViewParameterPrivilege { get; }

        /// <summary>
        /// Privilege required in order to (un)set this mode so long
        /// as they also have permission to view the mode and its
        /// parameter/list.
        /// </summary>
        string ModifyPrivilege { get; }

        /// <summary>
        /// Called when a user attempts to set the mode on a channel.
        /// May cause side effects of messages sent to the user, channel members, and/or server operators.
        /// </summary>
        /// <param name="user">User attempting to set the mode.</param>
        /// <param name="channel">Channel the mode is attempting to be set on.</param>
        /// <param name="parameter">Parameter specified when setting the mode, or <c>null</c> if no parameter was specified by the user.</param>
        /// <returns>Returns whether or not the mode was successfully set on the channel.</returns>
        bool TrySet(User user, Channel channel, string? parameter);

        /// <summary>
        /// Called when a user attempts to unset the mode on a channel.
        /// May cause side effects of messages sent to the user, channel members, and/or server operators.
        /// </summary>
        /// <param name="user">User attempting to set the mode.</param>
        /// <param name="channel">Channel the mode is attempting to be unset on.</param>
        /// <param name="parameter">Parameter specified when unsetting the mode, or <c>null</c> if no parameter was specified by the user.</param>
        /// <returns>Returns whether or not the mode was successfully unset on the channel.</returns>
        bool TryUnset(User user, Channel channel, string? parameter);
    }
}
