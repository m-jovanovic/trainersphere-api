using System.Net;
using Microsoft.Extensions.DependencyInjection;
using TrainerSphere.Api.Host;
using TrainerSphere.Api.Modules.Notifications.Domain;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Host;

public sealed class DemoSeedApiFactory(PostgresServer postgres) : ApiFactory(postgres)
{
    protected override void ConfigureSettings(IDictionary<string, string?> settings) => settings["Database:SeedDemoData"] = "true";
}

public sealed class DemoSeedTests(DemoSeedApiFactory factory) : IntegrationTest(factory), IClassFixture<DemoSeedApiFactory>
{
    [Fact]
    public async Task DemoTrainer_CanLogIn()
    {
        TestUser coach = await LoginAsync("coach@trainersphere.dev", "Demo123!");

        coach.Role.ShouldBe("trainer");
        coach.FirstName.ShouldBe("Maya");
        coach.LastName.ShouldBe("Novak");
    }

    [Theory]
    [InlineData("alex@trainersphere.dev")]
    [InlineData("sam@trainersphere.dev")]
    [InlineData("jordan@trainersphere.dev")]
    public async Task DemoClients_CanLogIn_AndBelongToTheDemoTrainer(string email)
    {
        TestUser coach = await LoginAsync("coach@trainersphere.dev", "Demo123!");

        TestUser client = await LoginAsync(email, "Demo123!");

        client.Role.ShouldBe("client");
        client.TrainerId.ShouldBe(coach.Id);
    }

    [Fact]
    public async Task DemoTrainer_HasThreeAcceptedInvitationsAndOnePendingForRiley()
    {
        TestUser coach = await LoginAsync("coach@trainersphere.dev", "Demo123!");

        Paged<Invitation> invitations = await (await coach.Client.GetAsync("/api/invitations", Ct)).ShouldHaveJsonAsync<Paged<Invitation>>();

        invitations.Items.Count(i => i.Status == "accepted").ShouldBe(3);
        Invitation pending = invitations.Items.Where(i => i.Status == "pending").ShouldHaveSingleItem();
        pending.Email.ShouldBe("riley@trainersphere.dev");
    }

    [Fact]
    public async Task PendingInvitation_EmailWithWorkingLinkWasSent()
    {
        await Factory.WaitForMessagesAsync();

        EmailMessage email = Factory.Emails.Single("riley@trainersphere.dev", EmailKinds.Invitation);
        string token = email.TextBody.Split("http://web.test/invite/")[1].Split(Environment.NewLine)[0].Trim();

        InvitationPreview preview = await (await CreateAnonymousClient().GetAsync($"/api/invitations/{token}", Ct)).ShouldHaveJsonAsync<InvitationPreview>();
        preview.Status.ShouldBe("pending");
        preview.TrainerName.ShouldBe("Maya Novak");
    }

    [Fact]
    public async Task Seed_RunAgain_AddsNothing()
    {
        DemoSeedStartupTask seed = Factory.Services.GetServices<IStartupTask>().OfType<DemoSeedStartupTask>().Single();
        int before = await Factory.QuerySingleAsync<int>("SELECT count(*)::int FROM identity.users");

        await seed.ExecuteAsync(Ct);

        (await Factory.QuerySingleAsync<int>("SELECT count(*)::int FROM identity.users")).ShouldBe(before);
        (await Factory.QuerySingleAsync<int>("SELECT count(*)::int FROM identity.invitations")).ShouldBe(4);
    }

    [Fact]
    public async Task SeededEvents_AreFullyProcessed()
    {
        await Should.NotThrowAsync(() => Factory.WaitForMessagesAsync(TimeSpan.FromSeconds(5)));

        (await Factory.QuerySingleAsync<int>("SELECT count(*)::int FROM notifications.recipients")).ShouldBe(4);
        HttpResponseMessage health = await CreateAnonymousClient().GetAsync("/health/ready", Ct);
        await health.ShouldHaveStatusAsync(HttpStatusCode.OK);
    }
}
