namespace TrainerSphere.SharedKernel.Results;

internal static class ResultGuard
{
    // A union is a struct, so default(Result<T>) (uninitialized fields, array slots) holds neither case.
    public static InvalidOperationException Uninitialized() =>
        new("The result is uninitialized: default(Result<T>) is neither a success nor a failure.");
}
