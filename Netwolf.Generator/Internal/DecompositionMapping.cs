// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Collections.Immutable;

namespace Netwolf.Generator.Internal;

public record DecompositionMapping(string Class, int From, ImmutableArray<int> To);
