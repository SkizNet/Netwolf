// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.Context;

using System.ComponentModel.DataAnnotations;

namespace Netwolf.Transport.IRC;

/// <summary>
/// Interface for the creation of <see cref="ValidationContext"/> instances.
/// This is used when validating command parameters in the process of dispatching commands.
/// If unset, a default ValidationContext will be created that is disassociated with
/// any DI service provider. If validation attributes require DI services, this interface
/// should be passed along to created <see cref="IContext"/> instances.
/// </summary>
public interface IValidationContextFactory
{
    /// <summary>
    /// Creates a new <see cref="ValidationContext"/> instance.
    /// </summary>
    /// <param name="instance">The object to validate.</param>
    /// <param name="items">Optional dictionary of items to pass to the validation context.</param>
    /// <returns>A new <see cref="ValidationContext"/> instance.</returns>
    ValidationContext Create(object instance, IDictionary<object, object?>? items = null);
}
