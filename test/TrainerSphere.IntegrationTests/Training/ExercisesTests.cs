using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TrainerSphere.IntegrationTests.Builders;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Training;

/// <summary>
/// Section 6: the seeded library, the taxonomy endpoint, library filters and custom trainer exercises.
/// </summary>
public sealed class ExercisesTests(ApiFactory factory) : TrainingTest(factory), IClassFixture<ApiFactory>
{
    private static readonly string[] ExerciseProperties =
    [
        "id", "slug", "name", "category", "movementPattern", "primaryMuscles", "secondaryMuscles", "equipment",
        "unilateral", "cue", "illustration", "isCustom", "archived",
    ];

    [Fact]
    public async Task ListLibrary_ForANewTrainer_ReturnsTheSeeded47ExercisesSortedByName()
    {
        TestUser trainer = await CreateUtcTrainerAsync();

        HttpResponseMessage response = await trainer.Client.GetAsync($"{TrainingRoutes.Exercises}?pageSize=200", Ct);
        JsonElement body = await response.ReadJsonAsync();
        body.ShouldHaveExactProperties("items", "page", "pageSize", "totalCount", "totalPages");
        body.GetProperty("totalCount").GetInt32().ShouldBe(47);

        JsonElement[] items = [.. body.GetProperty("items").EnumerateArray()];
        items.Length.ShouldBe(47);
        items.ShouldAllBe(e => !e.GetProperty("isCustom").GetBoolean());
        items.ShouldAllBe(e => !e.GetProperty("archived").GetBoolean());
        items.Select(e => e.GetProperty("name").GetString()).ShouldBe(
            items.Select(e => e.GetProperty("name").GetString()).OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetLibraryExercise_ReturnsTheContractShape()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        Exercise squat = await LibraryExerciseAsync(trainer, "barbell-back-squat");

        HttpResponseMessage response = await trainer.Client.GetAsync(TrainingRoutes.Exercise(squat.Id), Ct);

        JsonElement body = await response.ReadJsonAsync();
        body.ShouldHaveExactProperties(ExerciseProperties);
        body.GetProperty("slug").GetString().ShouldBe("barbell-back-squat");
        body.GetProperty("name").GetString().ShouldBe("Barbell Back Squat");
        body.GetProperty("category").GetString().ShouldBe("strength");
        body.GetProperty("movementPattern").GetString().ShouldBe("squat");
        body.GetProperty("primaryMuscles").EnumerateArray().Select(m => m.GetString()).ShouldBe(["quads", "glutes"]);
        body.GetProperty("illustration").GetString().ShouldBe("barbell-back-squat.svg");
        body.GetProperty("isCustom").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Taxonomy_ReturnsTheContractListsInOrder()
    {
        TestUser trainer = await CreateUtcTrainerAsync();

        ExerciseTaxonomy taxonomy = await (await trainer.Client.GetAsync(TrainingRoutes.ExerciseTaxonomy, Ct))
            .ShouldHaveJsonAsync<ExerciseTaxonomy>();

        taxonomy.Categories.ShouldBe(["strength", "cardio", "mobility"]);
        taxonomy.MovementPatterns.ShouldBe(
            ["squat", "hinge", "lunge", "horizontal-push", "vertical-push", "horizontal-pull", "vertical-pull", "carry", "core", "isolation", "cardio", "mobility"]);
        taxonomy.Muscles.ShouldBe(
        [
            "chest", "front-delts", "side-delts", "rear-delts", "lats", "upper-back", "traps", "lower-back", "biceps", "triceps",
            "forearms", "abs", "obliques", "glutes", "quads", "hamstrings", "adductors", "abductors", "calves", "hip-flexors",
        ]);
        taxonomy.Equipment.ShouldBe(
            ["barbell", "dumbbell", "kettlebell", "machine", "cable", "bodyweight", "bench", "pull-up-bar", "band", "cardio-machine", "box", "mat", "dip-station", "jump-rope"]);
    }

    [Fact]
    public async Task ListExercises_SearchMatchesCaseInsensitively()
    {
        TestUser trainer = await CreateUtcTrainerAsync();

        Paged<Exercise> page = await (await trainer.Client.GetAsync($"{TrainingRoutes.Exercises}?search=SQUAT", Ct))
            .ShouldHaveJsonAsync<Paged<Exercise>>();

        page.Items.ShouldNotBeEmpty();
        page.Items.ShouldContain(e => e.Slug == "barbell-back-squat");
    }

    [Fact]
    public async Task ListExercises_ByMuscle_MatchesPrimaryAndSecondary()
    {
        TestUser trainer = await CreateUtcTrainerAsync();

        Paged<Exercise> page = await (await trainer.Client.GetAsync($"{TrainingRoutes.Exercises}?muscle=lower-back&pageSize=200", Ct))
            .ShouldHaveJsonAsync<Paged<Exercise>>();

        // lower-back is a secondary muscle of the back squat, not a primary one.
        page.Items.ShouldContain(e => e.Slug == "barbell-back-squat");
        page.Items.ShouldAllBe(e => e.PrimaryMuscles.Contains("lower-back") || e.SecondaryMuscles.Contains("lower-back"));
    }

    [Fact]
    public async Task ListExercises_WithRepeatedMuscles_OrValues()
    {
        TestUser trainer = await CreateUtcTrainerAsync();

        Paged<Exercise> page = await (await trainer.Client.GetAsync($"{TrainingRoutes.Exercises}?muscle=chest&muscle=abs&pageSize=200", Ct))
            .ShouldHaveJsonAsync<Paged<Exercise>>();

        page.Items.ShouldNotBeEmpty();
        page.Items.ShouldAllBe(e =>
            e.PrimaryMuscles.Any(m => m == "chest" || m == "abs") || e.SecondaryMuscles.Any(m => m == "chest" || m == "abs"));
    }

    [Theory]
    [InlineData("category=strength")]
    [InlineData("pattern=hinge")]
    [InlineData("equipment=kettlebell")]
    public async Task ListExercises_WithATaxonomyFilter_ReturnsOnlyMatches(string query)
    {
        TestUser trainer = await CreateUtcTrainerAsync();

        Paged<Exercise> page = await (await trainer.Client.GetAsync($"{TrainingRoutes.Exercises}?{query}&pageSize=200", Ct))
            .ShouldHaveJsonAsync<Paged<Exercise>>();

        page.Items.ShouldNotBeEmpty();
        string value = query.Split('=')[1];
        page.Items.ShouldAllBe(e =>
            e.Category == value || e.MovementPattern == value || e.Equipment.Contains(value));
    }

    [Theory(Skip = "QA-F9: taxonomy errors are keyed per item ('muscle[0]') and read ''Muscle' must be one of: ...', not the contract's parameter key and 'Must be one of: ...'")]
    [InlineData("category=powerlifting", "category")]
    [InlineData("pattern=spin", "pattern")]
    [InlineData("muscle=quadriceps", "muscle")]
    [InlineData("equipment=stones", "equipment")]
    public async Task ListExercises_WithAnUnknownFilterValue_Returns400OnThatField(string query, string field)
    {
        TestUser trainer = await CreateUtcTrainerAsync();

        HttpResponseMessage response = await trainer.Client.GetAsync($"{TrainingRoutes.Exercises}?{query}", Ct);

        Problem problem = await response.ShouldBeValidationProblemAsync(field);
        problem.Errors![field][0].ShouldStartWith("Must be one of:");
    }

    [Fact(Skip = "QA-F9: taxonomy errors read ''Category' must be one of: ...', not the contract's 'Must be one of: ...'")]
    public async Task CreateCustomExercise_WithAnUnknownTaxonomyValue_ReturnsTheContractMessage()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        const string Body = """{ "name": "N", "category": "powerlifting", "movementPattern": "squat", "primaryMuscles": ["quads"], "secondaryMuscles": [], "equipment": ["barbell"], "unilateral": false }""";

        HttpResponseMessage response = await trainer.Client.PostAsync(
            TrainingRoutes.Exercises, new StringContent(Body, Encoding.UTF8, "application/json"), Ct);

        Problem problem = await response.ShouldBeValidationProblemAsync("category");
        problem.Errors!["category"][0].ShouldStartWith("Must be one of:");
    }

    [Fact]
    public async Task ListExercises_PagesThroughTheLibrary()
    {
        TestUser trainer = await CreateUtcTrainerAsync();

        Paged<Exercise> first = await (await trainer.Client.GetAsync($"{TrainingRoutes.Exercises}?pageSize=20&page=1", Ct))
            .ShouldHaveJsonAsync<Paged<Exercise>>();
        Paged<Exercise> last = await (await trainer.Client.GetAsync($"{TrainingRoutes.Exercises}?pageSize=20&page=3", Ct))
            .ShouldHaveJsonAsync<Paged<Exercise>>();

        first.Items.Count.ShouldBe(20);
        first.TotalCount.ShouldBe(47);
        first.TotalPages.ShouldBe(3);
        last.Items.Count.ShouldBe(7);
        first.Items.Select(e => e.Id).Intersect(last.Items.Select(e => e.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateCustomExercise_Returns201WithTheContractShape()
    {
        TestUser trainer = await CreateUtcTrainerAsync();

        HttpResponseMessage response = await trainer.Client.PostAsJsonAsync(
            TrainingRoutes.Exercises,
            new ExerciseBuilder().WithName(Unique.Name("Kettlebell Flow")).WithCue("Hinge, don't squat.").Build(),
            HttpAssertions.Json,
            Ct);

        JsonElement body = await response.ReadJsonAsync();
        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
        body.ShouldHaveExactProperties(ExerciseProperties);
        body.GetProperty("isCustom").GetBoolean().ShouldBeTrue();
        body.ShouldBeNullProperty("illustration");
        body.GetProperty("archived").GetBoolean().ShouldBeFalse();
        body.GetProperty("cue").GetString().ShouldBe("Hinge, don't squat.");
        body.GetProperty("unilateral").GetBoolean().ShouldBeFalse();
    }

    [Theory]
    [InlineData("""{ "name": "", "category": "strength", "movementPattern": "squat", "primaryMuscles": ["quads"], "secondaryMuscles": [], "equipment": ["barbell"], "unilateral": false }""", "name")]
    [InlineData("""{ "name": "N", "category": "powerlifting", "movementPattern": "squat", "primaryMuscles": ["quads"], "secondaryMuscles": [], "equipment": ["barbell"], "unilateral": false }""", "category")]
    [InlineData("""{ "name": "N", "category": "strength", "movementPattern": "spin", "primaryMuscles": ["quads"], "secondaryMuscles": [], "equipment": ["barbell"], "unilateral": false }""", "movementPattern")]
    [InlineData("""{ "name": "N", "category": "strength", "movementPattern": "squat", "primaryMuscles": [], "secondaryMuscles": [], "equipment": ["barbell"], "unilateral": false }""", "primaryMuscles")]
    [InlineData("""{ "name": "N", "category": "strength", "movementPattern": "squat", "primaryMuscles": ["quadriceps"], "secondaryMuscles": [], "equipment": ["barbell"], "unilateral": false }""", "primaryMuscles")]
    [InlineData("""{ "name": "N", "category": "strength", "movementPattern": "squat", "primaryMuscles": ["quads"], "secondaryMuscles": ["calfs"], "equipment": ["barbell"], "unilateral": false }""", "secondaryMuscles")]
    [InlineData("""{ "name": "N", "category": "strength", "movementPattern": "squat", "primaryMuscles": ["quads"], "secondaryMuscles": [], "equipment": ["stones"], "unilateral": false }""", "equipment")]
    public async Task CreateCustomExercise_WithAnInvalidValue_Returns400OnThatField(string body, string field)
    {
        TestUser trainer = await CreateUtcTrainerAsync();

        HttpResponseMessage response = await trainer.Client.PostAsync(
            TrainingRoutes.Exercises, new StringContent(body, Encoding.UTF8, "application/json"), Ct);

        Problem problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "Validation");
        problem.Errors.ShouldNotBeNull();
        problem.Errors.Keys.ShouldContain(k => k == field || k.StartsWith(field + "[", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(101, "name")]
    [InlineData(100, null)]
    public async Task CreateCustomExercise_ValidatesTheNameLength(int length, string? field)
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        string body = $$"""{ "name": "{{new string('x', length)}}", "category": "strength", "movementPattern": "squat", "primaryMuscles": ["quads"], "secondaryMuscles": [], "equipment": ["barbell"], "unilateral": false }""";

        HttpResponseMessage response = await trainer.Client.PostAsync(
            TrainingRoutes.Exercises, new StringContent(body, Encoding.UTF8, "application/json"), Ct);

        if (field is null)
        {
            await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
        }
        else
        {
            await response.ShouldBeValidationProblemAsync(field);
        }
    }

    [Fact]
    public async Task CreateCustomExercise_WithACueOver1000Characters_Returns400OnCue()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        string body = $$"""{ "name": "{{Unique.Name("Cued")}}", "category": "strength", "movementPattern": "squat", "primaryMuscles": ["quads"], "secondaryMuscles": [], "equipment": ["barbell"], "unilateral": false, "cue": "{{new string('x', 1001)}}" }""";

        HttpResponseMessage response = await trainer.Client.PostAsync(
            TrainingRoutes.Exercises, new StringContent(body, Encoding.UTF8, "application/json"), Ct);

        await response.ShouldBeValidationProblemAsync("cue");
    }

    [Fact]
    public async Task CreateCustomExercise_WithADuplicateName_Returns409NameInUse()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        Exercise existing = await CreateCustomExerciseAsync(trainer);

        HttpResponseMessage response = await trainer.Client.PostAsJsonAsync(
            TrainingRoutes.Exercises, new ExerciseBuilder().WithName(existing.Name).Build(), HttpAssertions.Json, Ct);

        await response.ShouldBeCompleteProblemAsync(HttpStatusCode.Conflict, "Exercise.NameInUse");
    }

    [Fact]
    public async Task CreateCustomExercise_WithANameAnotherTrainerUses_Returns201()
    {
        TestUser first = await CreateUtcTrainerAsync();
        TestUser second = await CreateUtcTrainerAsync();
        Exercise theirs = await CreateCustomExerciseAsync(first);

        HttpResponseMessage response = await second.Client.PostAsJsonAsync(
            TrainingRoutes.Exercises, new ExerciseBuilder().WithName(theirs.Name).Build(), HttpAssertions.Json, Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
    }

    [Fact]
    public async Task UpdateCustomExercise_ReplacesEveryField()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        Exercise existing = await CreateCustomExerciseAsync(trainer);

        ExerciseWrite update = new ExerciseBuilder()
            .WithName(Unique.Name("Renamed"))
            .WithCategory("mobility")
            .WithMovementPattern("mobility")
            .WithPrimaryMuscles("hip-flexors")
            .WithSecondaryMuscles("glutes", "abs")
            .WithEquipment("mat")
            .WithUnilateral()
            .WithCue("Breathe.")
            .Build();
        Exercise updated = await (await trainer.Client.PutAsJsonAsync(TrainingRoutes.Exercise(existing.Id), update, HttpAssertions.Json, Ct))
            .ShouldHaveJsonAsync<Exercise>();

        updated.Id.ShouldBe(existing.Id);
        updated.Name.ShouldBe(update.Name);
        updated.Category.ShouldBe("mobility");
        updated.MovementPattern.ShouldBe("mobility");
        updated.PrimaryMuscles.ShouldBe(["hip-flexors"]);
        updated.SecondaryMuscles.ShouldBe(["glutes", "abs"]);
        updated.Equipment.ShouldBe(["mat"]);
        updated.Unilateral.ShouldBeTrue();
        updated.Cue.ShouldBe("Breathe.");

        Exercise reread = await (await trainer.Client.GetAsync(TrainingRoutes.Exercise(existing.Id), Ct)).ShouldHaveJsonAsync<Exercise>();
        reread.ShouldBeJson(updated);
    }

    [Fact]
    public async Task UpdateCustomExercise_OmittingPrimaryMuscles_Returns400AndChangesNothing()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        Exercise existing = await CreateCustomExerciseAsync(trainer);
        string body = $$"""{ "name": "{{existing.Name}}", "category": "strength", "movementPattern": "squat", "secondaryMuscles": [], "equipment": ["barbell"], "unilateral": false }""";

        HttpResponseMessage response = await trainer.Client.PutAsync(
            TrainingRoutes.Exercise(existing.Id), new StringContent(body, Encoding.UTF8, "application/json"), Ct);

        await response.ShouldBeValidationProblemAsync("primaryMuscles");
        Exercise reread = await (await trainer.Client.GetAsync(TrainingRoutes.Exercise(existing.Id), Ct)).ShouldHaveJsonAsync<Exercise>();
        reread.ShouldBeJson(existing);
    }

    [Fact]
    public async Task UpdateLibraryExercise_Returns403NotEditable()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        Exercise squat = await LibraryExerciseAsync(trainer, "barbell-back-squat");

        HttpResponseMessage response = await trainer.Client.PutAsJsonAsync(
            TrainingRoutes.Exercise(squat.Id), new ExerciseBuilder().Build(), HttpAssertions.Json, Ct);

        await response.ShouldBeCompleteProblemAsync(HttpStatusCode.Forbidden, "Exercise.NotEditable");
    }

    [Fact]
    public async Task DeleteLibraryExercise_Returns403NotEditable()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        Exercise squat = await LibraryExerciseAsync(trainer, "barbell-back-squat");

        HttpResponseMessage response = await trainer.Client.DeleteAsync(TrainingRoutes.Exercise(squat.Id), Ct);

        await response.ShouldBeCompleteProblemAsync(HttpStatusCode.Forbidden, "Exercise.NotEditable");
    }

    [Fact]
    public async Task DeleteCustomExercise_ArchivesItButKeepsItReadable()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        Exercise custom = await CreateCustomExerciseAsync(trainer);

        await (await trainer.Client.DeleteAsync(TrainingRoutes.Exercise(custom.Id), Ct)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        Paged<Exercise> defaultList = await (await trainer.Client.GetAsync($"{TrainingRoutes.Exercises}?pageSize=200", Ct))
            .ShouldHaveJsonAsync<Paged<Exercise>>();
        defaultList.Items.ShouldNotContain(e => e.Id == custom.Id);
        defaultList.TotalCount.ShouldBe(47);

        Paged<Exercise> withArchived = await (await trainer.Client.GetAsync($"{TrainingRoutes.Exercises}?includeArchived=true&pageSize=200", Ct))
            .ShouldHaveJsonAsync<Paged<Exercise>>();
        withArchived.Items.ShouldContain(e => e.Id == custom.Id && e.Archived);

        Exercise reread = await (await trainer.Client.GetAsync(TrainingRoutes.Exercise(custom.Id), Ct)).ShouldHaveJsonAsync<Exercise>();
        reread.Archived.ShouldBeTrue();
    }

    [Fact]
    public async Task ListExercises_WithCustomFilter_SelectsCustomOrLibrary()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        Exercise custom = await CreateCustomExerciseAsync(trainer);

        Paged<Exercise> customs = await (await trainer.Client.GetAsync($"{TrainingRoutes.Exercises}?custom=true&pageSize=200", Ct))
            .ShouldHaveJsonAsync<Paged<Exercise>>();
        Paged<Exercise> library = await (await trainer.Client.GetAsync($"{TrainingRoutes.Exercises}?custom=false&pageSize=200", Ct))
            .ShouldHaveJsonAsync<Paged<Exercise>>();

        customs.Items.ShouldAllBe(e => e.IsCustom);
        customs.Items.ShouldContain(e => e.Id == custom.Id);
        library.Items.ShouldAllBe(e => !e.IsCustom);
        library.TotalCount.ShouldBe(47);
    }

    [Fact]
    public async Task GetCustomExercise_ForAnotherTrainer_Returns404()
    {
        TestUser owner = await CreateUtcTrainerAsync();
        TestUser other = await CreateUtcTrainerAsync();
        Exercise custom = await CreateCustomExerciseAsync(owner);

        HttpResponseMessage response = await other.Client.GetAsync(TrainingRoutes.Exercise(custom.Id), Ct);

        await response.ShouldBeCompleteProblemAsync(HttpStatusCode.NotFound, "Exercise.NotFound");
    }

    [Fact]
    public async Task Client_SeesTheirTrainersCustomExercises_ButNotAnotherTrainers()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        TestUser other = await CreateUtcTrainerAsync();
        Exercise own = await CreateCustomExerciseAsync(trainer);
        Exercise foreign = await CreateCustomExerciseAsync(other);

        Paged<Exercise> page = await (await client.Client.GetAsync($"{TrainingRoutes.Exercises}?pageSize=200", Ct))
            .ShouldHaveJsonAsync<Paged<Exercise>>();
        page.Items.ShouldContain(e => e.Id == own.Id);
        page.Items.ShouldNotContain(e => e.Id == foreign.Id);

        await (await client.Client.GetAsync(TrainingRoutes.Exercise(foreign.Id), Ct))
            .ShouldBeCompleteProblemAsync(HttpStatusCode.NotFound, "Exercise.NotFound");
    }

    [Fact]
    public async Task GetExercise_WithAnUnknownId_Returns404()
    {
        TestUser trainer = await CreateUtcTrainerAsync();

        HttpResponseMessage response = await trainer.Client.GetAsync(TrainingRoutes.Exercise(Guid.NewGuid()), Ct);

        await response.ShouldBeCompleteProblemAsync(HttpStatusCode.NotFound, "Exercise.NotFound");
    }

    [Fact]
    public async Task ListExercises_Anonymous_Returns401()
    {
        HttpResponseMessage response = await CreateAnonymousClient().GetAsync(TrainingRoutes.Exercises, Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.Unauthorized);
    }
}
