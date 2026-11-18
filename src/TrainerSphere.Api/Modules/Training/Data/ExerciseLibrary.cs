using System.Text.Json;
using TrainerSphere.Api.Host;
using TrainerSphere.Api.Modules.Training.Domain;
using TrainerSphere.SharedKernel.Data;
using TrainerSphere.SharedKernel.Domain;

namespace TrainerSphere.Api.Modules.Training.Data;

internal sealed record LibraryExercise(
    string Slug,
    string Name,
    string Category,
    string MovementPattern,
    string[] PrimaryMuscles,
    string[] SecondaryMuscles,
    string[] Equipment,
    bool Unilateral,
    string? Cue,
    string Illustration);

/// <summary>
/// The illustrated system exercise library and taxonomy, read from the embedded <c>exercises.json</c>.
/// </summary>
internal sealed class ExerciseLibrary
{
    public const string ResourceName = "TrainerSphere.Api.Modules.Training.Data.Seed.exercises.json";

    // Library ids are version 5 UUIDs of this namespace and the slug; changing it changes every library id.
    public static readonly Guid IdNamespace = new("6f1d2c3b-8a4e-4b7f-9c2d-5e8a1b3c7d90");

    private static readonly JsonSerializerOptions SeedOptions = new(JsonSerializerDefaults.Web);

    private ExerciseLibrary(ExerciseTaxonomy taxonomy, IReadOnlyList<LibraryExercise> exercises)
    {
        Taxonomy = taxonomy;
        Exercises = exercises;
    }

    public ExerciseTaxonomy Taxonomy { get; }

    public IReadOnlyList<LibraryExercise> Exercises { get; }

    public static Guid IdFor(string slug) => Ids.FromName(IdNamespace, slug);

    public static ExerciseLibrary LoadEmbedded()
    {
        using Stream stream = typeof(ExerciseLibrary).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded resource '{ResourceName}' is missing.");

        return Parse(stream);
    }

    public static ExerciseLibrary Parse(Stream json)
    {
        SeedFile file = JsonSerializer.Deserialize<SeedFile>(json, SeedOptions)
            ?? throw new InvalidOperationException("The exercise seed file is empty.");

        return new ExerciseLibrary(file.Taxonomy, file.Exercises);
    }

    internal sealed record SeedFile(ExerciseTaxonomy Taxonomy, IReadOnlyList<LibraryExercise> Exercises);
}

/// <summary>
/// Upserts the system exercises by slug at startup, after the migrations, so every environment has the same
/// library with the same ids.
/// </summary>
internal sealed class ExerciseLibrarySeeder(IDbSession db, ExerciseLibrary library, TimeProvider timeProvider) : IStartupTask
{
    public int Order => 10;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        DateTime now = timeProvider.UtcNow();

        await db.BeginTransactionAsync(cancellationToken);
        await db.ExecuteAsync(
            """
            INSERT INTO training.exercises (id, trainer_id, slug, name, category, movement_pattern, primary_muscles, secondary_muscles, equipment, unilateral, cue, illustration, archived, created_at, updated_at)
            VALUES (@Id, NULL, @Slug, @Name, @Category, @MovementPattern, @PrimaryMuscles, @SecondaryMuscles, @Equipment, @Unilateral, @Cue, @Illustration, false, @Now, @Now)
            ON CONFLICT (slug) WHERE slug IS NOT NULL DO UPDATE
            SET name = EXCLUDED.name, category = EXCLUDED.category, movement_pattern = EXCLUDED.movement_pattern,
                primary_muscles = EXCLUDED.primary_muscles, secondary_muscles = EXCLUDED.secondary_muscles, equipment = EXCLUDED.equipment,
                unilateral = EXCLUDED.unilateral, cue = EXCLUDED.cue, illustration = EXCLUDED.illustration, updated_at = EXCLUDED.updated_at
            WHERE (exercises.name, exercises.category, exercises.movement_pattern, exercises.primary_muscles, exercises.secondary_muscles,
                   exercises.equipment, exercises.unilateral, exercises.cue, exercises.illustration)
                  IS DISTINCT FROM (EXCLUDED.name, EXCLUDED.category, EXCLUDED.movement_pattern, EXCLUDED.primary_muscles, EXCLUDED.secondary_muscles,
                   EXCLUDED.equipment, EXCLUDED.unilateral, EXCLUDED.cue, EXCLUDED.illustration)
            """,
            library.Exercises.Select(e => new
            {
                Id = ExerciseLibrary.IdFor(e.Slug),
                e.Slug,
                e.Name,
                e.Category,
                e.MovementPattern,
                e.PrimaryMuscles,
                e.SecondaryMuscles,
                e.Equipment,
                e.Unilateral,
                e.Cue,
                e.Illustration,
                Now = now,
            }).ToList(),
            cancellationToken);
        await db.CommitAsync(cancellationToken);
    }
}
