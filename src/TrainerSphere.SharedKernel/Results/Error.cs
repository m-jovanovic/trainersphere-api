namespace TrainerSphere.SharedKernel.Results;

/// <summary>
/// The failure case of <see cref="Result{T}"/>. The hierarchy is closed, so switches over it are exhaustive.
/// </summary>
/// <param name="Code">A stable machine-readable code such as <c>Client.NotFound</c>.</param>
/// <param name="Message">A human-readable description.</param>
public closed record Error(string Code, string Message);

/// <summary>The requested resource does not exist or is not visible to the caller.</summary>
public sealed record NotFoundError(string Code, string Message) : Error(Code, Message);

/// <summary>The request conflicts with the current state, for example a duplicate email.</summary>
public sealed record ConflictError(string Code, string Message) : Error(Code, Message);

/// <summary>A domain rule rejected an otherwise valid request.</summary>
public sealed record DomainError(string Code, string Message) : Error(Code, Message);

/// <summary>The caller is not authenticated or presented invalid credentials.</summary>
public sealed record UnauthorizedError(string Code, string Message) : Error(Code, Message);

/// <summary>The caller is authenticated but not allowed to perform the operation.</summary>
public sealed record ForbiddenError(string Code, string Message) : Error(Code, Message);

/// <summary>An expected but unrecoverable failure.</summary>
public sealed record UnexpectedError(string Code, string Message) : Error(Code, Message);

/// <summary>A single invalid input field.</summary>
/// <param name="Field">The camelCase JSON path of the field, for example <c>exercises[0].sets</c>.</param>
/// <param name="Code">The error code.</param>
/// <param name="Message">The error message.</param>
public sealed record FieldError(string Field, string Code, string Message);

/// <summary>One or more input fields are invalid.</summary>
public sealed record ValidationError : Error
{
    public const string ValidationCode = "Validation";

    private readonly FieldError[] _errors;

    public ValidationError(params IEnumerable<FieldError> errors)
        : base(ValidationCode, "One or more validation errors occurred.")
    {
        ArgumentNullException.ThrowIfNull(errors);

        _errors = [.. errors];

        if (_errors.Length == 0)
        {
            throw new ArgumentException("At least one field error is required.", nameof(errors));
        }
    }

    public IReadOnlyList<FieldError> Errors => _errors;

    public static ValidationError For(string field, string message, string? code = null) =>
        new(new FieldError(field, code ?? ValidationCode, message));

    public bool Equals(ValidationError? other) =>
        other is not null && _errors.SequenceEqual(other._errors);

    public override int GetHashCode() =>
        _errors.Aggregate(ValidationCode.GetHashCode(StringComparison.Ordinal), HashCode.Combine);

    public ValidationError Merge(ValidationError other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return new ValidationError(_errors.Concat(other._errors));
    }
}
