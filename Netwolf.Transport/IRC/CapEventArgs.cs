using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Transport.IRC;

/// <summary>
/// Arguments for CAP-related events
/// </summary>
public class CapEventArgs : EventArgs
{
    /// <summary>
    /// Network the event was raised for. Read-only.
    /// </summary>
    public INetwork Network { get; init; }

    /// <summary>
    /// Capability name. Read-only.
    /// </summary>
    public string CapName { get; init; }

    /// <summary>
    /// Capability value. Read-only, will only be populated during CapReceived,
    /// and may be null for that event as well if the network did not specify a value for it.
    /// </summary>
    public string? CapValue { get; init; }

    /// <summary>
    /// The CAP subcommand, will be one of LS or NEW for CapReceived, LIST or ACK for CapEnabled,
    /// and DEL for CapDisabled. No event is raised for NAK, so Subcommand will never be NAK.
    /// </summary>
    public string Subcommand { get; init; }

    /// <summary>
    /// For the CapReceived event, controls whether we will CAP REQ this capability once
    /// the event handler finishes running. If true, we will request the capability from
    /// the network. This property is unused for all other cap events, and setting it will
    /// have no effect for them.
    /// </summary>
    public bool EnableCap { get; set; }

    /// <summary>
    /// Cancellation token to use for any asynchronous tasks awaited by the event.
    /// </summary>
    public CancellationToken Token { get; init; }

    /// <summary>
    /// Construct a new CapEventArgs
    /// </summary>
    /// <param name="network"></param>
    /// <param name="capName"></param>
    /// <param name="subcommand"></param>
    /// <param name="token"></param>
    public CapEventArgs(INetwork network, string capName, string? capValue, string subcommand, CancellationToken token)
    {
        Network = network;
        CapName = capName;
        CapValue = capValue;
        Subcommand = subcommand;
        Token = token;
    }
}
