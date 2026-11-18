using FsCheck;
using FsCheck.Xunit;
using TrainerSphere.Api.Modules.Training.Domain;

namespace TrainerSphere.UnitTests.Training.Domain;

public sealed class PersonalRecordProperties
{
    [Property]
    public bool EstimatedOneRepMax_never_decreases_with_more_reps(PositiveInt weight, PositiveInt reps)
    {
        int r = (reps.Get % PersonalRecords.MaxRepsForEstimate) + 1;
        int next = Math.Min(r + 1, PersonalRecords.MaxRepsForEstimate);
        decimal? a = PersonalRecords.EstimatedOneRepMax(weight.Get, r);
        decimal? b = PersonalRecords.EstimatedOneRepMax(weight.Get, next);

        return a is not null && b is not null && a <= b;
    }

    [Property]
    public bool EstimatedOneRepMax_has_at_most_two_decimals(PositiveInt weight, PositiveInt reps)
    {
        int r = (reps.Get % PersonalRecords.MaxRepsForEstimate) + 1;
        decimal? value = PersonalRecords.EstimatedOneRepMax(weight.Get, r);

        return value is null || value.Value == decimal.Round(value.Value, 2);
    }

    [Property(MaxTest = 50)]
    public bool Detect_reports_at_most_one_record_per_exercise_and_type(NonNegativeInt[] weights, PositiveInt[] reps)
    {
        (LoggedSet[] session, LoggedSet[] history) = Sets(weights, reps);

        return PersonalRecords.Detect(session, history)
            .GroupBy(r => (r.ExerciseId, r.Type))
            .All(group => group.Count() == 1);
    }

    [Property(MaxTest = 50)]
    public bool Every_reported_record_beats_its_previous_value(NonNegativeInt[] weights, PositiveInt[] reps)
    {
        (LoggedSet[] session, LoggedSet[] history) = Sets(weights, reps);

        return PersonalRecords.Detect(session, history).All(r => r.Value > r.PreviousValue);
    }

    private static (LoggedSet[] Session, LoggedSet[] History) Sets(NonNegativeInt[] weights, PositiveInt[] reps)
    {
        Guid[] exerciseIds = [Guid.NewGuid(), Guid.NewGuid()];
        LoggedSet[] all =
        [
            .. weights.Zip(reps).Select(
                (pair, index) => new LoggedSet(
                    new Guid(index, 0, 0, [1, 2, 3, 4, 5, 6, 7, 8]),
                    exerciseIds[index % exerciseIds.Length],
                    pair.First.Get == 0 ? null : pair.First.Get,
                    pair.Second.Get)),
        ];
        int half = all.Length / 2;

        return (all[..half], all[half..]);
    }
}
