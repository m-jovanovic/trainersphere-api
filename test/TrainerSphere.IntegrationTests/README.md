# Integration tests

Black-box tests of the real API host (`Program`, middleware, all modules) against Postgres in Docker
(Testcontainers). Docker must be running.

```powershell
dotnet test --project test/TrainerSphere.IntegrationTests
dotnet test --project test/TrainerSphere.IntegrationTests -- --filter-class "*SignupTests"
```

## How the harness works

- `Infrastructure/PostgresServer` is an assembly fixture: one `postgres:18-alpine` container per test run.
- `Infrastructure/ApiFactory` is a class fixture: every test class gets its own host and its own **empty database**.
  Startup runs the migrations, so every fixture also proves migrations apply to an empty database. Test classes run
  in parallel; tests inside a class run sequentially and share the database.
- Test settings: auth rate limit effectively off, message polling every 50 ms, `MaxAttempts` 3, demo seed off,
  OpenAPI on, `App:WebBaseUrl` = `http://web.test`, logging at Warning.
- `Factory.Events` records every integration event published by the API. It is how tests get one-time tokens that
  only ever leave the API by email (`InvitationIssued.Token`, `PasswordResetRequested.Token`).
- `Factory.WaitForMessagesAsync()` waits until every module's outbox and inbox is drained and fails with the handler
  error if a message cannot be processed. Call it before asserting asynchronous effects (Training client profiles
  created from `ClientRegistered`, emails).
- `Factory.QueryAsync/QuerySingleAsync/ExecuteAsync` run SQL against the fixture database.

## Conventions

**Folders** mirror the API modules and slices:

```
Host/                         migrations, health, OpenAPI, error pipeline, harness self-tests
Identity/<Slice>Tests.cs      SignupTests, LoginTests, RefreshTests, LogoutTests, MeTests, PasswordTests, InvitationTests ...
Training/<Slice>Tests.cs      ExerciseTests, WorkoutTemplateTests, ProgramTests, AssignmentTests, SessionTests ...
Notifications/<...>Tests.cs
Builders/<Module>Builders.cs  fluent request builders with unique defaults
Contracts/<Module>.cs         test-side request/response records
Infrastructure/               fixtures and helpers (change only with the backend lead)
```

**Test classes** are `public sealed`, use a primary constructor, derive from `IntegrationTest` and declare their
fixture:

```csharp
public sealed class SignupTests(ApiFactory factory) : IntegrationTest(factory), IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Signup_WithTakenEmail_Returns409EmailInUse()
    {
        TestUser existing = await CreateTrainerAsync();

        HttpResponseMessage response = await CreateAnonymousClient().PostAsJsonAsync(
            "/api/auth/signup", new SignupBuilder().WithEmail(existing.Email).Build(), HttpAssertions.Json, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "User.EmailInUse");
    }
}
```

**Test names**: `Action_Condition_Outcome` (`AcceptInvitation_WhenCancelled_Returns409NotPending`).

**Arrange through the API**, the way a user would: `CreateTrainerAsync`, `CreateClientAsync(trainer)`,
`CreateTrainerWithClientsAsync(n)`, `InviteAsync(trainer)`, `LoginAsync(email, password)`. Each returns a
`TestUser` whose `Client` sends the bearer token and keeps the refresh cookie. Add new arrange helpers for other
modules as extension methods or protected methods next to the tests that need them; move them to `IntegrationTest`
once two modules use them. Use direct SQL only for states the API cannot reach (for example
`UPDATE identity.invitations SET expires_at = now() - interval '1 minute' WHERE id = @Id`).

**Data isolation**: the database is shared by the tests of a class, so always use builder defaults or `Unique`
values, and scope assertions to data the test created (its own trainer, its own ids). Never assert global counts.

**Wire format**: requests and responses use the records in `Contracts/`, never the API's internal types, so a
renamed property in the API breaks a test instead of silently changing the contract. Enums are strings with the
exact contract values (`"trainer"`, `"pending"`, `"kg"`). Serialize with `HttpAssertions.Json`.

**Assertions** (Shouldly):

- `await response.ShouldHaveJsonAsync<T>(HttpStatusCode.Created)`: status, `application/json`, body.
- `await response.ShouldHaveStatusAsync(HttpStatusCode.NoContent)`: includes the body in the failure message.
- `await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "Invitation.NotPending")`: RFC 9457 shape, `code`, `traceId`.
- `await response.ShouldBeValidationProblemAsync("email", "sets[2].reps")`: 400 `Validation` with those `errors` keys.

**Coverage per endpoint** (contract.md is the source of truth):

1. Happy path with the full response body checked against the contract.
2. 401 without a token; 403 for the wrong role (trainer-only vs client-only routes).
3. Ownership: another trainer's or another client's resource returns 404, never 403.
4. Validation: each rule produces 400 `Validation` with the camelCase JSON-path key.
5. Domain rules: every documented 409/422 code.
6. Side effects: events published (`Factory.Events`), async effects after `WaitForMessagesAsync`, rows only when the
   API offers no way to observe them.

**Non-default configuration** (for example a low auth rate limit): subclass `ApiFactory`, override
`ConfigureSettings` or `ConfigureServices`, and declare `IClassFixture<YourFactory>` on the test class.

**Async**: pass `Ct` (the xUnit test cancellation token) to every call that takes a token.
