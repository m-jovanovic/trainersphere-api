using Shared.Results;

namespace Modules.Training.Application.ValidationErrors;

/// <summary>
/// Contains the invitation validation errors.
/// </summary>
internal static class InvitationValidationErrors
{
    /// <summary>
    /// Gets the trainer identifier is required error.
    /// </summary>
    internal static Error TrainerIdentifierIsRequired => new("Invitation.TrainerIdentifierIsRequired", "The trainer identifier is required.");

    /// <summary>
    /// Gets the email is required error.
    /// </summary>
    internal static Error EmailIsRequired => new("Trainer.EmailIsRequired", "The email is required.");
}
