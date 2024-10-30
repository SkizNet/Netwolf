// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Netwolf.PluginFramework.Exceptions;

public class NoMatchingPermissionManagerException : InvalidOperationException
{
    public string CommandName { get; init; }
    public Type ContextType { get; init; }
    public string Permission { get; init; }

    public NoMatchingPermissionManagerException(string commandName, Type contextType, string permission)
        : base("No registered permission managers support the given context and permission")
    {
        CommandName = commandName;
        ContextType = contextType;
        Permission = permission;
    }
}
