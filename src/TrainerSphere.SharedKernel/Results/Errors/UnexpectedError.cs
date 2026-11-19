namespace TrainerSphere.SharedKernel.Results;

/// <summary>
/// An expected but unrecoverable failure, for example a downstream service being unavailable.
/// </summary>
/// <param name="Code">The error code.</param>
/// <param name="Message">The error message.</param>
public sealed record UnexpectedError(string Code, string Message) : Error(Code, Message);
