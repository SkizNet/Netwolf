// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.CodeAnalysis;
using Netwolf.Generator.Resources;

namespace Netwolf.Generator;

/// <summary>
/// Localizable diagonstic descriptors for source generators
/// </summary>
/// <remarks>
/// ID format: NETWOLFsccnn where s = source number, cc = category number, nn = diagnostic number
/// 
/// Sources
/// -------
/// 0 - General
/// 1 - PluginCommandGenerator
/// 
/// Categories
/// ----------
/// 00 - Design
/// 01 - Documentation
/// 02 - Globalization
/// 03 - Interoperability
/// 04 - Maintainability
/// 05 - Naming
/// 06 - Performance
/// 07 - SingleFile
/// 08 - Reliability
/// 09 - Security
/// 10 - Style
/// 11 - Usage
/// 
/// </remarks>
internal static class DiagnosticDescriptors
{
    internal static readonly DiagnosticDescriptor UnsupportedParameterType = new(
        "NETWOLF10001",
        new LocalizableResourceString(nameof(Diagnostics.UnsupportedParameterTypeTitle), Diagnostics.ResourceManager, typeof(Diagnostics)),
        new LocalizableResourceString(nameof(Diagnostics.UnsupportedParameterTypeMessageFormat), Diagnostics.ResourceManager, typeof(Diagnostics)),
        "Design",
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor UnsupportedParameterDefault = new(
        "NETWOLF10002",
        new LocalizableResourceString(nameof(Diagnostics.UnsupportedParameterDefaultTitle), Diagnostics.ResourceManager, typeof(Diagnostics)),
        new LocalizableResourceString(nameof(Diagnostics.UnsupportedParameterDefaultMessageFormat), Diagnostics.ResourceManager, typeof(Diagnostics)),
        "Design",
        DiagnosticSeverity.Warning,
        true);

    internal static readonly DiagnosticDescriptor InvalidCommandContext = new(
        "NETWOLF10003",
        new LocalizableResourceString(nameof(Diagnostics.InvalidCommandContextTitle), Diagnostics.ResourceManager, typeof(Diagnostics)),
        new LocalizableResourceString(nameof(Diagnostics.InvalidCommandContextMessageFormat), Diagnostics.ResourceManager, typeof(Diagnostics)),
        "Design",
        DiagnosticSeverity.Warning,
        true);

    internal static readonly DiagnosticDescriptor InvalidCommandName = new(
        "NETWOLF10501",
        new LocalizableResourceString(nameof(Diagnostics.InvalidCommandNameTitle), Diagnostics.ResourceManager, typeof(Diagnostics)),
        new LocalizableResourceString(nameof(Diagnostics.InvalidCommandNameMessageFormat), Diagnostics.ResourceManager, typeof(Diagnostics)),
        "Naming",
        DiagnosticSeverity.Warning,
        true);
}
