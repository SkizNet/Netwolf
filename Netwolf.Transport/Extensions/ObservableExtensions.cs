// Copyright (c) 2025 Ryan Schmidt <skizzerz@skizzerz.net>
// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Reactive.Linq;

namespace Netwolf.Transport.Extensions;

public static class ObservableExtensions
{
    /// <summary>
    /// Subscribes an asynchronous callback to an observable, while ensuring that exceptions
    /// thrown by the callback are properly propagated and that callbacks for subsequent results
    /// in the observable sequence are not started until the previous callback has completed.
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="observable"></param>
    /// <param name="callback"></param>
    /// <returns></returns>
    public static void SubscribeAsync<TResult>(this IObservable<TResult> observable, Func<Task> callback, CancellationToken token)
    {
        observable
            .Select(args => Observable.FromAsync(callback))
            .Concat()
            .Subscribe(token);
    }


    /// <summary>
    /// Subscribes an asynchronous callback to an observable, while ensuring that exceptions
    /// thrown by the callback are properly propagated and that callbacks for subsequent results
    /// in the observable sequence are not started until the previous callback has completed.
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="observable"></param>
    /// <param name="callback"></param>
    /// <returns></returns>
    public static void SubscribeAsync<TResult>(this IObservable<TResult> observable, Func<TResult, Task> callback, CancellationToken token)
    {
        observable
            .Select(args => Observable.FromAsync(async () => await callback(args)))
            .Concat()
            .Subscribe(token);
    }
}
