namespace TrainerSphere.IntegrationTests.Contracts;

// Test-side copies of the HTTP contract (section 4 and 5). They are deliberately not the API's own types, so
// renaming a property in the API breaks a test instead of silently changing the wire format.
public sealed record SignupRequest(string Email, string Password, string FirstName, string LastName, string? TimeZone);

public sealed record LoginRequest(string Email, string Password);

public sealed record UpdateMeRequest(string FirstName, string LastName, string Units, string TimeZone);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Token, string NewPassword);

public sealed record User(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    Guid? TrainerId,
    string Units,
    string TimeZone,
    DateTimeOffset CreatedAt);

public sealed record AuthResponse(string AccessToken, string TokenType, int ExpiresIn, User User);

public sealed record CreateInvitationRequest(string Email, string FirstName, string LastName, string? Message);

public sealed record AcceptInvitationRequest(string FirstName, string LastName, string Password, string? TimeZone);

public sealed record Invitation(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? Message,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? AcceptedAt,
    Guid? ClientId);

public sealed record InvitationPreview(
    string Email,
    string FirstName,
    string LastName,
    string TrainerName,
    string? Message,
    string Status,
    DateTimeOffset ExpiresAt);
