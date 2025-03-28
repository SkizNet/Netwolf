// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.PluginFramework.Loader;

public record PluginMetadata(
    int Id,
    string Name,
    string Description,
    string Version,
    string Path);
