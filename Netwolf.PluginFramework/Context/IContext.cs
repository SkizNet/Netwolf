// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.PluginFramework.Context;

/// <summary>
/// Defines a "context" surrounding a command invocation and
/// holds data about that context. switch expressions or
/// is expressions can be used to downcast this to the appropriate
/// underlying implementation type depending on the framework in use.
/// </summary>
public interface IContext
{
    /// <summary>
    /// The object this command is associated with and was
    /// responsible for dispatching this command.
    /// </summary>
    object Sender { get; }

    /// <summary>
    /// A means of obtaining a ValidationContext for parameter validation.
    /// </summary>
    IValidationContextFactory? ValidationContextFactory { get; }
}
