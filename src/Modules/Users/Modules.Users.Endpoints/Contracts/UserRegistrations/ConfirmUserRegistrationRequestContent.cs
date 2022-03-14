namespace Modules.Users.Endpoints.Contracts.UserRegistrations;

/// <summary>
/// Represents the confirm user registration request content.
/// </summary>
/// <param name="FirstName">The first name.</param>
/// <param name="LastName">The last name.</param>
public sealed record ConfirmUserRegistrationRequestContent(string FirstName, string LastName);
