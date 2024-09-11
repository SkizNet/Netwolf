﻿using Microsoft.Extensions.Logging;

using Netwolf.PluginFramework.Commands;
using Netwolf.PluginFramework.Context;
using Netwolf.Server.Exceptions;
using Netwolf.Server.Users;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Server.Commands;

public class CommandValidator : ICommandValidator<ICommandResponse>
{
    private ILogger Logger { get; init; }

    public CommandValidator(ILogger<CommandValidator> logger)
    {
        Logger = logger;
    }

    public void ValidateCommand(ICommand command, ICommandHandler<ICommandResponse> commandHandler, IContext context)
    {
        if (command.CommandType != CommandType.Client)
        {
            throw new ArgumentException("Not passed a client command", nameof(command));
        }

        var handler = (IServerCommandHandler)commandHandler;
        var ctx = (ServerContext)context;

        if (handler.HasChannel && ctx.Channel == null)
        {
            throw new NoSuchChannelException(command.Args[0]);
        }
    }

    public bool ValidateCommandHandler(ICommandHandler<ICommandResponse> commandHandler)
    {
        var type = commandHandler.GetType();
        var handler = (IServerCommandHandler)commandHandler;

        if (handler.Privilege != null)
        {
            if (handler.Privilege.Length < 6 || handler.Privilege[5] != ':')
            {
                Logger.LogWarning(@"Skipping {Type}: invalid privilege {Privilege}", type.FullName, handler.Privilege);
                return false;
            }

            var container = handler.Privilege.AsSpan()[..4];
            switch (container)
            {
                case "user":
                case "oper":
                    break;
                case "chan":
                    // For commands that require channel privileges, a channel must be the first parameter
                    // and it must not be optional, repeated, or a list
                    if (!handler.HasChannel)
                    {
                        Logger.LogWarning(@"Skipping {Type}: channel privilege specified but command lacks a channel", type.FullName);
                        return false;
                    }

                    break;
                default:
                    Logger.LogWarning(@"Skipping {Type}: invalid privilege scope", type.FullName);
                    return false;
            }
        }

        return true;
    }

    public bool ValidateCommandType(Type commandType)
    {
        return commandType.IsAssignableTo(typeof(IServerCommandHandler));
    }
}
