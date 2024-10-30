// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class CommandAttribute : Attribute
{
    public string Name { get; init; }

    public string? Privilege { get; init; }

    public CommandAttribute(string name, string? privilege = null)
    {
        Name = name;
        Privilege = privilege;
    }
}
