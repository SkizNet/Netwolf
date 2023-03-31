namespace Netwolf.Transport.Internal;

internal static class ExceptionHelper
{
    /// <summary>
    /// Evaluates the action, ignoring exceptions of type TException.
    /// </summary>
    /// <typeparam name="TException">Exception type to ignore</typeparam>
    /// <param name="action">Function to evaluate</param>
    internal static void Suppress<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            // ignore
        }
    }

    internal static async Task SuppressAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException)
        {
            // ignore
        }
    }
}
