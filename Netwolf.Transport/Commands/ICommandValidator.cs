// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.Transport.Context;

namespace Netwolf.Transport.Commands;

/// <summary>
/// Service to validate a command. The default implementation treats all commands as valid
/// </summary>
public interface ICommandValidator<TResult>
{
    /// <summary>
    /// Called before a Type implementing <typeparamref name="TResult"/> is instantiated
    /// to determine if it is a valid type. Will not be called for abstract types or unbound generics.
    /// </summary>
    /// <param name="commandType"></param>
    /// <returns>True if command is valid</returns>
    bool ValidateCommandType(Type commandType);

    /// <summary>
    /// After a type is considered valid, it will be instantiated. Then this callback is run
    /// to do additional validation on the constructed command handler.
    /// </summary>
    /// <param name="commandHandler"></param>
    /// <returns>True if command is valid</returns>
    bool ValidateCommandHandler(ICommandHandler<TResult> commandHandler);

    /// <summary>
    /// During runtime, we also validate the actual commands to ensure that they are supported
    /// by the dispatcher. Unlike the other Validate methods, this one should throw an exception
    /// when encountering an invalid command.
    /// </summary>
    /// <param name="command"></param>
    /// <param name="commandHandler"></param>
    /// <param name="context"></param>
    void ValidateCommand(ICommand command, ICommandHandler<TResult> commandHandler, IContext context);
}
