using TrainerSphere.Api.Modules.Training.Domain;
using TrainerSphere.Api.Modules.Training.Features.Assignments;
using TrainerSphere.Api.Modules.Training.Features.Clients;

namespace TrainerSphere.UnitTests.Training.Features;

public sealed class ValidatorTests
{
    [Theory]
    [InlineData("name")]
    [InlineData("-lastSessionAt")]
    [InlineData("adherence7d")]
    public void Client_list_accepts_the_contract_sorts(string sort)
    {
        ListClientsValidator validator = new();

        validator.Validate(new ListClientsQuery(Viewer.Trainer(TrainingTestData.TrainerId), null, null, sort, 1, 20)).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Name")]
    [InlineData("email")]
    [InlineData("+name")]
    public void Client_list_rejects_unknown_sorts(string sort)
    {
        ListClientsValidator validator = new();

        FluentValidation.Results.ValidationResult result = validator.Validate(
            new ListClientsQuery(Viewer.Trainer(TrainingTestData.TrainerId), null, null, sort, 1, 20));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(f => f.PropertyName == "Sort");
    }

    [Theory]
    [InlineData("active", true)]
    [InlineData("archived", true)]
    [InlineData("Active", false)]
    [InlineData("ARCHIVED", false)]
    public void Client_list_status_is_a_strict_kebab_enum(string? status, bool valid)
    {
        ListClientsValidator validator = new();

        validator.Validate(new ListClientsQuery(Viewer.Trainer(TrainingTestData.TrainerId), null, status, null, 1, 20)).IsValid.ShouldBe(valid);
    }

    [Theory]
    [InlineData("scheduled", true)]
    [InlineData("missed", true)]
    [InlineData("Scheduled", false)]
    [InlineData("done", false)]
    public void Assignment_list_status_is_a_strict_kebab_enum(string? status, bool valid)
    {
        ListAssignmentsValidator validator = new();

        validator.Validate(new ListAssignmentsQuery(Viewer.Trainer(TrainingTestData.TrainerId), null, null, null, status)).IsValid.ShouldBe(valid);
    }

    [Theory]
    [InlineData(0, 20, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 101, false)]
    [InlineData(1, 100, true)]
    public void Paging_is_bounded(int page, int pageSize, bool valid)
    {
        ListClientsValidator validator = new();

        validator.Validate(new ListClientsQuery(Viewer.Trainer(TrainingTestData.TrainerId), null, null, null, page, pageSize)).IsValid.ShouldBe(valid);
    }
}
