namespace TrainerSphere.SharedKernel.Results;

/// <summary>
/// A single invalid input field.
/// </summary>
/// <param name="Field">The camelCase JSON path of the field in the request body, for example <c>email</c> or <c>sets[2].reps</c>.</param>
/// <param name="Code">The error code.</param>
/// <param name="Message">The error message.</param>
public sealed record FieldError(string Field, string Code, string Message);
