namespace TrainerSphere.SharedKernel.Results;

/// <summary>
/// The request conflicts with the current state, for example a duplicate email.
/// </summary>
/// <param name="Code">The error code.</param>
/// <param name="Message">The error message.</param>
public sealed record ConflictError(string Code, string Message) : Error(Code, Message);
