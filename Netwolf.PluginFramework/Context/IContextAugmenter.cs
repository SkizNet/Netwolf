// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Netwolf.PluginFramework.Commands;

namespace Netwolf.PluginFramework.Context;

/// <summary>
/// Services to augment an <see cref="IContext"/> with additional data.
/// These are called in series as a pipeline; augmenters should examine the context
/// type being passed in, and if it is a supported type, add whichever data they desire to it.
/// They should then return the resulting context (or the original context if it is an unsupported type).
/// </summary>
public interface IContextAugmenter
{
    IContext AugmentForCommand(IContext context, ICommand command, ICommandHandler handler);
}
