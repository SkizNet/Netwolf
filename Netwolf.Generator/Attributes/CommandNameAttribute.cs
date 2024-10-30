// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.Generator.Attributes;

/// <summary>
/// When this attribute is used on a parameter of type string
/// (or a type that string can be converted to) in a Command handler,
/// that parameter will be populated with the name of the command that
/// triggered the handler call.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class CommandNameAttribute : Attribute
{
}
