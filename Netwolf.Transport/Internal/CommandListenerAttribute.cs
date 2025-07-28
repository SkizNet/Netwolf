// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.Transport.Internal;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal class CommandListenerAttribute : Attribute
{
    // nothing here; this attribute exists solely for source generator performance
}
