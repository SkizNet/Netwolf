// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace System.Runtime.CompilerServices;

// Helper class that we are required to define ourselves due to targeting netstandard2.0
// (which is a requirement for source generators)
// Defining this allows us to use record types in this assembly
internal static class IsExternalInit { }
