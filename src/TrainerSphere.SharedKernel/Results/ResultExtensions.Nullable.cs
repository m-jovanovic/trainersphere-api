namespace TrainerSphere.SharedKernel.Results;

/// <content>
/// Conversions from nullable values, for example a Dapper <c>QuerySingleOrDefaultAsync</c> that found no row.
/// </content>
public static partial class ResultExtensions
{
    public static Result<T> ToResult<T>(this T? value, Error error)
        where T : class =>
        value is null ? error : new Success<T>(value);

    public static Result<T> ToResult<T>(this T? value, Error error)
        where T : struct =>
        value.HasValue ? new Success<T>(value.Value) : error;

    public static async Task<Result<T>> ToResult<T>(this Task<T?> source, Error error)
        where T : class =>
        (await source).ToResult(error);

    public static async Task<Result<T>> ToResult<T>(this Task<T?> source, Error error)
        where T : struct =>
        (await source).ToResult(error);
}
