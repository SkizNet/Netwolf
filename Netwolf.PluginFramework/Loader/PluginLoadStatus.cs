// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.PluginFramework.Loader;

public enum PluginLoadStatus
{
    /// <summary>
    /// The operation was successful
    /// </summary>
    Success,
    /// <summary>
    /// A load operation failed because the plugin at that path was already loaded
    /// </summary>
    AlreadyLoaded,
    /// <summary>
    /// A load operation failed because the target file does not exist or we don't have permission to read/execute it
    /// </summary>
    FileNotFound,
    /// <summary>
    /// An unload operation failed because no plugin with that ID is loaded
    /// </summary>
    NotLoaded,
    /// <summary>
    /// The load operation failed because the plugin uses a Netwolf framework not present in the base application
    /// </summary>
    FrameworkMismatch,
    /// <summary>
    /// The load operation failed because the plugin was built against an incompatible version of Netwolf
    /// </summary>
    VersionMismatch,
}
