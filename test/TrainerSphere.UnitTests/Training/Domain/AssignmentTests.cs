using TrainerSphere.Api.Modules.Training.Domain;
using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.UnitTests.Training.Domain;

public sealed class AssignmentTests
{
    [Fact]
    public void A_scheduled_assignment_dated_before_today_shows_as_missed()
    {
        Assignment assignment = TrainingTestData.ScheduledAssignment(date: TrainingTestData.Today.AddDays(-1));

        assignment.StatusOn(TrainingTestData.Today).ShouldBe(AssignmentStatus.Missed);
        assignment.StoredStatus.ShouldBe(AssignmentStatus.Scheduled);
    }

    [Fact]
    public void A_scheduled_assignment_dated_today_is_still_scheduled()
    {
        Assignment assignment = TrainingTestData.ScheduledAssignment(date: TrainingTestData.Today);

        assignment.StatusOn(TrainingTestData.Today).ShouldBe(AssignmentStatus.Scheduled);
    }

    [Fact]
    public void Rescheduling_a_completed_assignment_is_a_conflict()
    {
        Assignment assignment = TrainingTestData.ScheduledAssignment();
        assignment.Complete(TrainingTestData.Now, TrainingTestData.Now);

        Result<Assignment> result = assignment.Reschedule(TrainingTestData.Today.AddDays(1), null, TrainingTestData.Now);

        ConflictError error = result.Value.ShouldBeOfType<ConflictError>();
        error.Code.ShouldBe("Assignment.NotScheduled");
    }

    [Fact]
    public void A_completed_assignment_reports_its_session_in_every_conflict()
    {
        Assignment assignment = TrainingTestData.ScheduledAssignment();
        var sessionId = Guid.NewGuid();
        assignment.StartSession(sessionId, TrainingTestData.Now);
        assignment.Complete(TrainingTestData.Now, TrainingTestData.Now);

        Result<Assignment>[] results =
        [
            assignment.EnsureDeletable(),
            assignment.Skip(null, TrainingTestData.Now),
            assignment.Reschedule(TrainingTestData.Today.AddDays(1), null, TrainingTestData.Now),
        ];

        foreach (Result<Assignment> result in results)
        {
            ConflictError error = result.Value.ShouldBeOfType<ConflictError>();
            error.Code.ShouldBe("Assignment.NotScheduled");
            error.Extensions.ShouldNotBeNull()["sessionId"].ShouldBe(sessionId);
        }
    }

    [Fact]
    public void An_assignment_with_a_session_cannot_be_deleted_and_reports_the_session()
    {
        Assignment assignment = TrainingTestData.ScheduledAssignment();
        var sessionId = Guid.NewGuid();
        assignment.StartSession(sessionId, TrainingTestData.Now);

        Result<Assignment> result = assignment.EnsureDeletable();

        ConflictError error = result.Value.ShouldBeOfType<ConflictError>();
        error.Code.ShouldBe("Assignment.NotScheduled");
        error.Extensions.ShouldNotBeNull()["sessionId"].ShouldBe(sessionId);
    }

    [Fact]
    public void Skipping_a_scheduled_assignment_records_the_reason()
    {
        Assignment assignment = TrainingTestData.ScheduledAssignment();

        Result<Assignment> result = assignment.Skip("  Felt ill  ", TrainingTestData.Now);

        result.Value.ShouldBeOfType<Success<Assignment>>();
        assignment.StoredStatus.ShouldBe(AssignmentStatus.Skipped);
        assignment.SkipReason.ShouldBe("Felt ill");
        assignment.SkippedAt.ShouldBe(TrainingTestData.Now);
    }

    [Fact]
    public void Skipping_twice_is_a_conflict()
    {
        Assignment assignment = TrainingTestData.ScheduledAssignment();
        assignment.Skip(null, TrainingTestData.Now);

        assignment.Skip(null, TrainingTestData.Now).Value.ShouldBeOfType<ConflictError>();
    }

    [Fact]
    public void Starting_a_second_session_is_a_conflict_with_the_first_session_id()
    {
        Assignment assignment = TrainingTestData.ScheduledAssignment();
        var first = Guid.NewGuid();
        assignment.StartSession(first, TrainingTestData.Now);

        Result<Assignment> result = assignment.StartSession(Guid.NewGuid(), TrainingTestData.Now);

        ConflictError error = result.Value.ShouldBeOfType<ConflictError>();
        error.Code.ShouldBe("Session.AlreadyStartedForAssignment");
        error.Extensions.ShouldNotBeNull()["sessionId"].ShouldBe(first);
    }

    [Fact]
    public void Discarding_the_session_frees_the_assignment_again()
    {
        Assignment assignment = TrainingTestData.ScheduledAssignment();
        assignment.StartSession(Guid.NewGuid(), TrainingTestData.Now);

        assignment.DetachSession(TrainingTestData.Now);

        assignment.SessionId.ShouldBeNull();
        assignment.EnsureDeletable().Value.ShouldBeOfType<Success<Assignment>>();
    }

    [Fact]
    public void Only_the_owning_trainer_and_the_client_see_the_assignment()
    {
        Assignment assignment = TrainingTestData.ScheduledAssignment();

        assignment.IsAccessibleBy(Viewer.Trainer(TrainingTestData.TrainerId)).ShouldBeTrue();
        assignment.IsAccessibleBy(Viewer.Trainer(Guid.NewGuid())).ShouldBeFalse();
        assignment.IsAccessibleBy(Viewer.Client(TrainingTestData.ClientId, TrainingTestData.TrainerId)).ShouldBeTrue();
        assignment.IsAccessibleBy(Viewer.Client(Guid.NewGuid(), TrainingTestData.TrainerId)).ShouldBeFalse();
    }
}
