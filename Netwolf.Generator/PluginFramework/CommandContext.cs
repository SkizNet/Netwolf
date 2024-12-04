// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.Generator.PluginFramework;

internal readonly record struct CommandContext(
    ContextType ContextType,
    string ContainerType,
    string MethodName,
    bool IsMethodAsync,
    bool IsMethodVoid,
    ValueCollection<CommandParameterContext> Parameters,
    ValueCollection<CommandAttributeContext> Attributes)
{
    internal static readonly CommandContext Invalid = new(ContextType.Invalid, "", "", false, false, [], []);
}
