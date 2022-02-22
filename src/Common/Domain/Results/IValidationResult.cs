namespace Domain.Results;

/// <summary>
/// Represents the validation result containing an array of errors.
/// </summary>
public interface IValidationResult
{
    protected static readonly Error ValidationError = new("ValidationError.Default", "A validation problem occurred.");

    /// <summary>
    /// Gets the errors.
    /// </summary>
    Error[] Errors { get; }
}
