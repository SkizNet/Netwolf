// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: GPL-3.0-or-later

using Netwolf.BotFramework.Services;

namespace Netwolf.BotFramework.Internal;

internal class DefaultCapProvider : ICapProvider
{
    /// <summary>
    /// CAPs we unconditionally support at the Bot layer
    /// </summary>
    private static readonly string[] _unconditionalCaps = [
        "multi-prefix",
        "userhost-in-names",
        "extended-join",
        "account-notify",
        "away-notify",
        "chghost",
        "setname",
        "draft/channel-rename",
    ];

    public bool ShouldEnable(string cap, string? value)
    {
        return _unconditionalCaps.Contains(cap);
    }
}
