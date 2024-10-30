// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Netwolf.BotFramework.Internal;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
internal class ServerCommandAttribute : Attribute
{
    internal string Command { get; init; }

    internal ServerCommandAttribute(string command)
    {
        Command = command;
    }
}
