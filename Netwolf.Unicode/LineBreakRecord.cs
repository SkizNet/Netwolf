// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.Unicode;

/// <summary>
/// A record indicating one line out of text that has been line-wrapped.
/// </summary>
/// <param name="Line">The line</param>
/// <param name="IsHardBreak">If true, this line ends in a hard break. If false, it ends in a soft break (wrapping)</param>
public record LineBreakRecord(string Line, bool IsHardBreak);
