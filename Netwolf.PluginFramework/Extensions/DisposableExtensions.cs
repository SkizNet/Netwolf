// Copyright(c) 2025 Ryan Schmidt<skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.Extensions.Logging;

namespace Netwolf.PluginFramework.Extensions;

internal static class DisposableExtensions
{
    internal static void SafeDispose(this IDisposable? disposable, ILogger logger)
    {
        if (disposable == null)
        {
            return;
        }

        try
        {
            disposable.Dispose();
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Error disposing {TypeName}", disposable.GetType().FullName);
        }
    }
}
