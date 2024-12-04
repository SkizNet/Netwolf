// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.CodeAnalysis;

namespace Netwolf.Generator.Internal;

internal static class SymbolExtensions
{
    internal static string ToFullyQualifiedString(this ISymbol symbol)
    {
        return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }
}
