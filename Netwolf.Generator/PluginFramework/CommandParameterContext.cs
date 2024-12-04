// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.CodeAnalysis;

namespace Netwolf.Generator.PluginFramework;

internal readonly record struct CommandParameterContext(
    string Name,
    string ParameterType,
    ParameterClassification Classification,
    string? ConversionTemplate,
    bool HasExplicitDefault,
    string? ExplicitDefaultSyntax,
    Location Location);
