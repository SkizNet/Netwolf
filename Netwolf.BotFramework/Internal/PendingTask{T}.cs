using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.BotFramework.Internal;

internal class PendingTask<T> : ICancelable, IDisposable
{
    public TaskCompletionSource<T> Source { get; init; }

    internal ulong Key { get; set; }

    private readonly WeakReference<PendingTaskCollection> _parent;
    
    internal PendingTask(PendingTaskCollection parent, TaskCompletionSource<T> tcs)
    {
        Source = tcs;
        _parent = new(parent);
    }

    void ICancelable.Cancel() => Source.TrySetCanceled();

    public void Dispose()
    {
        Source.TrySetCanceled();
        if (_parent.TryGetTarget(out var collection))
        {
            collection.RemoveWithoutCancel(Key);
        }
    }
}
