namespace Shared.Extensions;

public static class FunctionalExtensions
{
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
