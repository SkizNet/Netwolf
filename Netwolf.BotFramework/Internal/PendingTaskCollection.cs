using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.BotFramework.Internal;

/// <summary>
/// A thread-safe collection of TaskCompletionSources
/// </summary>
internal class PendingTaskCollection : IDisposable
{
    private ulong _pendingOperationKey = 0;

    private ConcurrentDictionary<ulong, ICancelable> _pendingOperations = [];

    internal void RemoveWithoutCancel(ulong key)
    {
        _pendingOperations.TryRemove(key, out _);
    }

    /// <summary>
    /// Create a new pending task
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <returns></returns>
    public PendingTask<TResult> Create<TResult>()
    {
        TaskCompletionSource<TResult> source = new(TaskCreationOptions.RunContinuationsAsynchronously);
        PendingTask<TResult> wrapper = new(this, source);
        ulong nextKey;

        do
        {
            nextKey = Interlocked.Increment(ref _pendingOperationKey);
        } while (!_pendingOperations.TryAdd(nextKey, wrapper));

        wrapper.Key = nextKey;
        return wrapper;
    }

    /// <summary>
    /// Create a new pending task
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <returns></returns>
    public PendingTask Create()
    {
        TaskCompletionSource source = new(TaskCreationOptions.RunContinuationsAsynchronously);
        PendingTask wrapper = new(this, source);
        ulong nextKey;

        do
        {
            nextKey = Interlocked.Increment(ref _pendingOperationKey);
        } while (!_pendingOperations.TryAdd(nextKey, wrapper));

        wrapper.Key = nextKey;
        return wrapper;
    }

    /// <summary>
    /// Cancel all pending tasks and free up resources
    /// </summary>
    public void Dispose()
    {
        // make a snapshot and set the field to null so other threads trying to insert
        // new tasks will fail with an exception (this indicates a bug in the library
        // consumer)
        var ops = _pendingOperations;
        _pendingOperations = null!;

        foreach (var (_, op) in ops)
        {
            // The PendingTasks implement IDisposable too but their Dispose call tries
            // to remove themselves from our collection in addition to canceling.
            // We're clearing the collection anyway so just call Cancel instead of Dispose.
            op.Cancel();
        }

        ops.Clear();
    }
}
