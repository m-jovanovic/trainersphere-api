namespace TrainerSphere.SharedKernel.Results;

/// <summary>
/// The caller is not authenticated.
/// </summary>
/// <param name="Code">The error code.</param>
/// <param name="Message">The error message.</param>
public sealed record UnauthorizedError(string Code, string Message) : Error(Code, Message);
