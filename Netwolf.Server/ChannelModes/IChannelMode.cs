using System.Collections.Generic;

namespace Netwolf.Server.ChannelModes
{
    /// <summary>
    /// API surface for channel modes.
    /// Don't directly implement this interface in other assemblies;
    /// extend one of the channel mode classes instead to avoid lots of
    /// boilerplate and potential implementation/security issues.
    /// </summary>
    public interface IChannelMode
    {
        /// <summary>
        /// Character representing this mode in a mode line.
        /// </summary>
        char ModeChar { get; }

        /// <summary>
        /// How this mode handles parameters.
        /// </summary>
        ParameterType ParameterType { get; }

        /// <summary>
        /// Parameter value for this mode, in a format suitable for client display.
        /// </summary>
        string? Parameter { get; }

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
        /// Set the mode on the given channel, or modify the mode's parameter
        /// if it is already set.
        /// <para>
        /// Side effects: Giving the user error messages if the mode cannot be set.
        /// </para>
        /// </summary>
        /// <param name="user">User attempting to set the mode.</param>
        /// <param name="channel">Channel mode is being set on.</param>
        /// <param name="parameter">User-specified parameter, if any.</param>
        /// <returns><c>true</c> if the mode was successfully set on the channel.</returns>
        bool Set(User user, Channel channel, string? parameter);

        /// <summary>
        /// Remove the mode from the given channel.
        /// <para>
        /// Side effects: Giving the user error messages if the mode cannot be unset.
        /// </para>
        /// </summary>
        /// <param name="user">User attempting to unset the mode.</param>
        /// <param name="channel">Channel the mode is being removed from.</param>
        /// <param name="parameter">User-specified parameter, if any.</param>
        /// <returns><c>true</c> if the mode was successfully removed from the channel.</returns>
        bool Unset(User user, Channel channel, string? parameter);
    }
}
