namespace TrainerSphere.SharedKernel.Results;

/// <summary>
/// The requested resource does not exist.
/// </summary>
/// <param name="Code">The error code.</param>
/// <param name="Message">The error message.</param>
public sealed record NotFoundError(string Code, string Message) : Error(Code, Message);
