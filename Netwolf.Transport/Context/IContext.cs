// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.IRC;

namespace Netwolf.Transport.Context;

/// <summary>
/// Defines a "context" surrounding a command invocation and
/// holds data about that context. switch expressions or
/// is expressions can be used to downcast this to the appropriate
/// underlying implementation type depending on the framework in use.
/// <para/>
/// Custom context implementations should extend <see cref="ExtensibleContextBase"/>
/// which in turn implements this interface to avoid some boilerplate.
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

    /// <summary>
    /// Check if any context augmenter has set extension data for
    /// the specified type into this context.
    /// </summary>
    /// <typeparam name="T">Extension data type</typeparam>
    /// <returns>true if extension data has been defined, false if it has not</returns>
    bool HasExtensionData<T>() where T : class;

    /// <summary>
    /// Retrieve the extension data for the specified type.
    /// </summary>
    /// <typeparam name="T">Extension data type</typeparam>
    /// <returns>The extension data for that type</returns>
    /// <exception cref="KeyNotFoundException">When extension data of the specified type is not defined</exception>
    T? GetExtensionData<T>() where T : class;

    /// <summary>
    /// Retrieve the extension data for the specified type, or a default
    /// value if the extension data is not defined.
    /// </summary>
    /// <typeparam name="T">Extension data type</typeparam>
    /// <param name="defaultValue">Default value to return if no extension data is defined for this type</param>
    /// <returns></returns>
    T? GetExtensionData<T>(T? defaultValue) where T : class;

    /// <summary>
    /// Sets the extension data for a specified type to the specified value,
    /// overwriting any previous values for this type.
    /// </summary>
    /// <typeparam name="T">Extension data type</typeparam>
    /// <param name="value">Value to set</param>
    /// <exception cref="InvalidOperationException">When extension data is frozen and can no longer be manipulated</exception>
    void SetExtensionData<T>(T? value) where T : class;

    /// <summary>
    /// Removes extension data for the specified type, returning it to not being defined.
    /// </summary>
    /// <typeparam name="T">Extension data type</typeparam>
    /// <exception cref="InvalidOperationException">When extension data is frozen and can no longer be manipulated</exception>
    void ClearExtensionData<T>() where T : class;

    /// <summary>
    /// Freezes all extension data, making it immutable.
    /// Extension data is only mutable during the IContextAugmenter service invocation.
    /// Once a context is passed to callbacks, it is no longer mutable.
    /// </summary>
    void FreezeExtensionData();
}
