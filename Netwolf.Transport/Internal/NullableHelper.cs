// Copyright (c) 2024 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Netwolf.Transport.Internal;

internal static class NullableHelper
{
    internal static ValueTask DisposeAsyncIfNotNull(IAsyncDisposable? obj)
    {
        return obj?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}
