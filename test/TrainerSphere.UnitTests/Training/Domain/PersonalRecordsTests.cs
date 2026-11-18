using TrainerSphere.Api.Modules.Training.Domain;

namespace TrainerSphere.UnitTests.Training.Domain;

public sealed class PersonalRecordsTests
{
    private static readonly Guid ExerciseId = Guid.NewGuid();

    [Fact]
    public void EstimatedOneRepMax_uses_the_epley_formula()
    {
        PersonalRecords.EstimatedOneRepMax(80m, 8).ShouldBe(101.33m);
        PersonalRecords.EstimatedOneRepMax(80m, 1).ShouldBe(80m);
        PersonalRecords.EstimatedOneRepMax(100.5m, 12).ShouldBe(140.70m);
    }

    [Fact]
    public void EstimatedOneRepMax_rejects_sets_that_do_not_qualify()
    {
        PersonalRecords.EstimatedOneRepMax(null, 8).ShouldBeNull();
        PersonalRecords.EstimatedOneRepMax(0, 8).ShouldBeNull();
        PersonalRecords.EstimatedOneRepMax(80, 13).ShouldBeNull();
        PersonalRecords.EstimatedOneRepMax(80, null).ShouldBeNull();
    }

    [Fact]
    public void First_logged_session_sets_the_baseline_and_reports_nothing()
    {
        LoggedSet[] session = [Set(80, 8), Set(85, 5)];

        PersonalRecords.Detect(session, []).ShouldBeEmpty();
    }

    [Fact]
    public void Beating_the_previous_best_one_rep_max_is_reported()
    {
        LoggedSet[] history = [Set(80, 8)]; // 101.33
        LoggedSet[] session = [Set(85, 8)]; // 107.33

        PersonalRecordCandidate record = PersonalRecords.Detect(session, history).Single(r => r.Type == PersonalRecordType.Estimated1Rm);

        record.Value.ShouldBe(107.67m);
        record.PreviousValue.ShouldBe(101.33m);
        record.WeightKg.ShouldBe(85m);
        record.Reps.ShouldBe(8);
    }

    [Fact]
    public void Max_weight_only_counts_sets_with_reps()
    {
        LoggedSet[] history = [Set(100, 1)];
        LoggedSet[] session = [Set(105, 0), Set(102, 2)];

        PersonalRecordCandidate record = PersonalRecords.Detect(session, history).Single(r => r.Type == PersonalRecordType.MaxWeight);

        record.Value.ShouldBe(102m);
        record.PreviousValue.ShouldBe(100m);
    }

    [Fact]
    public void Max_reps_compares_against_sets_at_the_same_or_a_higher_weight()
    {
        LoggedSet[] history = [Set(100, 5)];
        LoggedSet[] session = [Set(80, 8)];

        PersonalRecordCandidate record = PersonalRecords.Detect(session, history).Single(r => r.Type == PersonalRecordType.MaxReps);

        record.Value.ShouldBe(8);
        record.PreviousValue.ShouldBe(5);
    }

    [Fact]
    public void Max_reps_does_not_beat_an_earlier_set_with_more_reps()
    {
        LoggedSet[] history = [Set(80, 8)];
        LoggedSet[] session = [Set(80, 5)];

        PersonalRecords.Detect(session, history).ShouldBeEmpty();
    }

    [Fact]
    public void Bodyweight_sets_count_with_zero_weight()
    {
        LoggedSet[] history = [Set(50, 10)];
        LoggedSet[] session = [Set(null, 15)];

        PersonalRecordCandidate record = PersonalRecords.Detect(session, history).Single(r => r.Type == PersonalRecordType.MaxReps);

        record.Value.ShouldBe(15);
        record.PreviousValue.ShouldBe(10);
        record.WeightKg.ShouldBeNull();
    }

    [Fact]
    public void Max_volume_compares_whole_sessions_of_the_exercise()
    {
        var older = Guid.NewGuid();
        LoggedSet[] history = [Set(80, 8, older), Set(80, 8, older)]; // 1280 in one session
        LoggedSet[] session = [Set(85, 8), Set(85, 8)]; // 1360

        PersonalRecordCandidate record = PersonalRecords.Detect(session, history).Single(r => r.Type == PersonalRecordType.MaxVolume);

        record.Value.ShouldBe(1360m);
        record.PreviousValue.ShouldBe(1280m);
    }

    [Fact]
    public void At_most_one_record_per_type_is_reported_for_an_exercise()
    {
        LoggedSet[] history = [Set(80, 5)];
        LoggedSet[] session = [Set(90, 5), Set(92.5m, 5)];

        PersonalRecordCandidate record = PersonalRecords.Detect(session, history).Single(r => r.Type == PersonalRecordType.MaxWeight);

        record.Value.ShouldBe(92.5m);
    }

    [Fact]
    public void Records_of_other_exercises_do_not_interfere()
    {
        var other = Guid.NewGuid();
        LoggedSet[] history = [Set(200, 3, exerciseId: other)];
        LoggedSet[] session = [Set(80, 5)];

        PersonalRecords.Detect(session, history).ShouldBeEmpty();
    }

    private static LoggedSet Set(decimal? weight, int? reps, Guid? sessionId = null, Guid? exerciseId = null) =>
        new(sessionId ?? Guid.NewGuid(), exerciseId ?? ExerciseId, weight, reps);
}
