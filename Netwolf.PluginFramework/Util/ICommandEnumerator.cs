// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Netwolf.PluginFramework.Util;

public interface ICommandEnumerator : IEnumerator<string>
{
    string Rest { get; }

    void MoveToEnd();
}
