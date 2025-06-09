// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.Transport.IRC;

public class CommandValidator<TResult> : ICommandValidator<TResult>
{
    public void ValidateCommand(ICommand command, ICommandHandler<TResult> commandHandler, IContext context) { }

    public bool ValidateCommandHandler(ICommandHandler<TResult> commandHandler)
    {
        return true;
    }

    public bool ValidateCommandType(Type commandType)
    {
        return true;
    }
}
