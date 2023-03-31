namespace Netwolf.Transport.Internal;

internal static class NullableHelper
{
    internal static ValueTask DisposeAsyncIfNotNull(IAsyncDisposable? obj)
    {
        return obj?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}
