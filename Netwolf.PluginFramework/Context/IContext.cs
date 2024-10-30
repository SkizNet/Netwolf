// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: GPL-3.0-or-later

namespace Netwolf.PluginFramework.Context;

/// <summary>
/// Defines a "context" (e.g. user, server, or channel) and
/// holds data about that context. switch expressions or
/// is expressions can be used to downcast this to the appropriate
/// underlying implementation type depending on the framework in use.
/// </summary>
public interface IContext
{
}
