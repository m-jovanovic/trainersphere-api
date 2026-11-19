namespace TrainerSphere.SharedKernel.Results;

/// <summary>
/// One or more input fields are invalid.
/// </summary>
public sealed record ValidationError : Error
{
    /// <summary>
    /// The code shared by all validation errors.
    /// </summary>
    public const string ValidationCode = "Validation";

    private readonly FieldError[] _errors;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationError"/> class.
    /// </summary>
    /// <param name="errors">The field errors; at least one is required.</param>
    public ValidationError(params IEnumerable<FieldError> errors)
        : base(ValidationCode, "The request is invalid.")
    {
        ArgumentNullException.ThrowIfNull(errors);

        _errors = [.. errors];

        if (_errors.Length == 0)
        {
            throw new ArgumentException("A validation error needs at least one field error.", nameof(errors));
        }
    }

    /// <summary>
    /// Gets the field errors.
    /// </summary>
    public IReadOnlyList<FieldError> Errors => _errors;

    /// <summary>
    /// Creates a validation error for a single field.
    /// </summary>
    /// <param name="field">The camelCase JSON path of the field, for example <c>sets[2].reps</c>.</param>
    /// <param name="message">The error message.</param>
    /// <param name="code">The error code; defaults to <see cref="ValidationCode"/>.</param>
    /// <returns>The validation error.</returns>
    public static ValidationError For(string field, string message, string code = ValidationCode) =>
        new(new FieldError(field, code, message));

    /// <summary>
    /// Merges several validation errors into one, keeping the field errors in order.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    /// <returns>The merged validation error.</returns>
    public static ValidationError Merge(IEnumerable<ValidationError> errors) => new(errors.SelectMany(error => error.Errors));

    /// <summary>
    /// Groups the field errors by field, in the shape of RFC 9457 <c>errors</c>.
    /// </summary>
    /// <returns>The messages per field.</returns>
    public Dictionary<string, string[]> ToDictionary() =>
        _errors.GroupBy(error => error.Field, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(error => error.Message).ToArray(), StringComparer.Ordinal);

    /// <inheritdoc />
    public bool Equals(ValidationError? other) => base.Equals(other) && _errors.SequenceEqual(other._errors);

    /// <inheritdoc />
    public override int GetHashCode() =>
        _errors.Aggregate(base.GetHashCode(), (hash, error) => HashCode.Combine(hash, error));
}
