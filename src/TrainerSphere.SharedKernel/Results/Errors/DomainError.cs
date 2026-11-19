namespace TrainerSphere.SharedKernel.Results;

/// <summary>
/// A domain rule rejected the operation, for example accepting an expired invitation.
/// </summary>
/// <param name="Code">The error code.</param>
/// <param name="Message">The error message.</param>
public sealed record DomainError(string Code, string Message) : Error(Code, Message);
