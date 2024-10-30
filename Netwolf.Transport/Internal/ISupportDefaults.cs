// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.Transport.Internal;

/// <summary>
/// Default values in the event of missing ISUPPORT tokens
/// </summary>
internal static class ISupportDefaults
{
    internal static readonly string DefaultCasemapping = "ascii";
    internal static readonly string DefaultChannelTypes = "#&";
    internal static readonly string DefaultChannelModes = "b,k,l,imnpst";
    internal static readonly string DefaultPrefix = "(ov)@+";
}
