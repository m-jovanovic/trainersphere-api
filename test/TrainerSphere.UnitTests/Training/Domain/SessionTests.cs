using TrainerSphere.Api.Modules.Training.Domain;
using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.UnitTests.Training.Domain;

public sealed class SessionTests
{
    [Fact]
    public void Completing_twice_is_idempotent_and_reports_it_changed_nothing()
    {
        var session = Session.Start(Guid.NewGuid(), TrainingTestData.Client(), assignment: null, "Run", TrainingTestData.Now, TrainingTestData.Now);
        DateTime completedAt = TrainingTestData.Now.AddHours(1);

        session.Complete(completedAt, "Good one", TrainingTestData.Now).Value.ShouldBeOfType<Success<bool>>().Value.ShouldBeTrue();
        session.Complete(completedAt.AddHours(1), null, TrainingTestData.Now).Value.ShouldBeOfType<Success<bool>>().Value.ShouldBeFalse();

        session.Status.ShouldBe(SessionStatus.Completed);
        session.CompletedAt.ShouldBe(completedAt);
        session.Notes.ShouldBe("Good one");
    }

    [Fact]
    public void Completion_before_the_start_is_a_validation_error()
    {
        var session = Session.Start(Guid.NewGuid(), TrainingTestData.Client(), assignment: null, "Run", TrainingTestData.Now, TrainingTestData.Now);

        Result<bool> result = session.Complete(TrainingTestData.Now.AddMinutes(-1), null, TrainingTestData.Now);

        ValidationError error = result.Value.ShouldBeOfType<ValidationError>();
        error.Errors.ShouldBe([new FieldError("completedAt", ValidationError.ValidationCode, "The completion time cannot be before the start time.")]);
        session.Status.ShouldBe(SessionStatus.InProgress);
    }

    [Fact]
    public void Totals_sum_only_completed_sets()
    {
        var sessionId = Guid.NewGuid();
        SessionSet[] sets =
        [
            Set(sessionId, 80, 8, completed: true),
            Set(sessionId, 80, 8, completed: true),
            Set(sessionId, 100, 3, completed: false),
            Set(sessionId, null, 20, completed: true),
        ];

        var totals = SessionTotals.Of(sets, TrainingTestData.Now, TrainingTestData.Now.AddMinutes(30));

        totals.TotalVolumeKg.ShouldBe(1280m);
        totals.CompletedSets.ShouldBe(3);
        totals.TotalReps.ShouldBe(36);
        totals.DurationSeconds.ShouldBe(1800);
    }

    [Fact]
    public void Totals_of_an_in_progress_session_have_no_duration()
    {
        var totals = SessionTotals.Of([], TrainingTestData.Now, completedAt: null);

        totals.DurationSeconds.ShouldBeNull();
    }

    private static SessionSet Set(Guid sessionId, decimal? weight, int reps, bool completed) =>
        new(Guid.NewGuid(), sessionId, Guid.NewGuid(), 1, 1, weight, reps, null, null, null, completed, null, TrainingTestData.Now);
}
