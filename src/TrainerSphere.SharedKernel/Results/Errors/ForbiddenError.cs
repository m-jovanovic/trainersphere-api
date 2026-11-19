namespace TrainerSphere.SharedKernel.Results;

/// <summary>
/// The caller is authenticated but not allowed to perform the operation.
/// </summary>
/// <param name="Code">The error code.</param>
/// <param name="Message">The error message.</param>
public sealed record ForbiddenError(string Code, string Message) : Error(Code, Message);
