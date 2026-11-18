using System.Net.Http.Headers;
using TrainerSphere.IntegrationTests.Contracts;

namespace TrainerSphere.IntegrationTests.Infrastructure;

/// <summary>
/// A signed-in user. <see cref="Client"/> sends the access token and keeps the refresh cookie, like a browser would.
/// </summary>
public sealed record TestUser(Guid Id, string Email, string Password, string Role, string FirstName, string LastName, Guid? TrainerId, string AccessToken, HttpClient Client)
{
    public bool IsTrainer => Role == "trainer";

    public static TestUser From(HttpClient client, AuthResponse auth, string password)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(auth);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        User user = auth.User;
        return new TestUser(user.Id, user.Email, password, user.Role, user.FirstName, user.LastName, user.TrainerId, auth.AccessToken, client);
    }
}
