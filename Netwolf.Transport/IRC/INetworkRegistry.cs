// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.Transport.IRC;

/// <summary>
/// Service that keeps track of all created Networks, allowing lookup by name.
/// It additionally contains events for when networks are created and destroyed.
/// </summary>
public interface INetworkRegistry
{
    /// <summary>
    /// Retrieves the network associated with the specified name.
    /// </summary>
    /// <param name="name">The name of the network to retrieve. Cannot be null.</param>
    /// <returns>An <see cref="INetwork"/> instance representing the network configuration if found; otherwise, <see
    /// langword="null"/>.</returns>
    INetwork? GetNetwork(string name);

    /// <summary>
    /// Retrieves all available networks.
    /// </summary>
    /// <returns>An enumerable collection of <see cref="INetwork"/> instances representing all available networks. The collection
    /// will be empty if no networks are available.</returns>
    IEnumerable<INetwork> GetAllNetworks();

    /// <summary>
    /// Occurs when a new network is created.
    /// </summary>
    /// <remarks>This event is triggered whenever a new instance of a network is successfully created.
    /// Subscribers can handle this event to perform actions such as logging or initializing network-related
    /// resources.</remarks>
    event EventHandler<INetwork>? NetworkCreated;

    /// <summary>
    /// Occurs when a network is destroyed.
    /// </summary>
    /// <remarks>This event is triggered whenever a network represented by an <see cref="INetwork"/> instance
    /// is destroyed. Subscribers can use this event to perform cleanup or update operations related to the network's
    /// destruction.</remarks>
    event EventHandler<INetwork>? NetworkDestroyed;
}
