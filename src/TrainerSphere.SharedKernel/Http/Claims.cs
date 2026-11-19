using System.Security.Claims;

namespace TrainerSphere.SharedKernel.Http;

public enum CallerRole
{
    Trainer,
    Client,
}

public static class Roles
{
    public const string Trainer = "trainer";
    public const string Client = "client";
}

public static class ClaimNames
{
    public const string Subject = "sub";
    public const string Email = "email";
    public const string Role = "role";
    public const string Name = "name";
    public const string TrainerId = "trainer_id";
}

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        string? value = principal.FindFirstValue(ClaimNames.Subject);
        return Guid.TryParse(value, out Guid id) ? id : throw new InvalidOperationException("The user has no valid 'sub' claim.");
    }

    public static string GetRole(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return principal.FindFirstValue(ClaimNames.Role) ?? throw new InvalidOperationException("The user has no 'role' claim.");
    }

    public static bool IsTrainer(this ClaimsPrincipal principal) => principal.GetRole() == Roles.Trainer;

    public static Guid? GetTrainerId(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return Guid.TryParse(principal.FindFirstValue(ClaimNames.TrainerId), out Guid id) ? id : null;
    }

    /// <summary>
    /// The caller as seen by module handlers: a user id plus a role.
    /// </summary>
    public static Caller ToCaller(this ClaimsPrincipal principal) =>
        new(principal.GetUserId(), principal.IsTrainer() ? CallerRole.Trainer : CallerRole.Client);
}

public sealed record Caller(Guid UserId, CallerRole Role)
{
    public bool IsTrainer => Role == CallerRole.Trainer;

    public bool IsClient => Role == CallerRole.Client;
}
