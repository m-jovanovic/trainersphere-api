namespace Shared.Extensions;

/// <summary>
/// Contains extension methods for supporting some functional patterns.
/// </summary>
public static class FunctionalExtensions
{
    /// <summary>
    /// Executes a try-catch-finally block with the specified actions.
    /// </summary>
    /// <param name="action">The action in the try block.</param>
    /// <param name="catchAction">The action in the catch block.</param>
    /// <param name="finallyAction">The action in the finally block.</param>
    public static void TryCatchFinally(Action action, Action<Exception> catchAction, Action finallyAction)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            catchAction(exception);
        }
        finally
        {
            finallyAction();
        }
    }
}
