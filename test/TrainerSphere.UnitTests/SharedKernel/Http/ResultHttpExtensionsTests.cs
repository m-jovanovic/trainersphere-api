using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using TrainerSphere.SharedKernel.Http;
using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.UnitTests.SharedKernel.Http;

public sealed class ResultHttpExtensionsTests
{
    public static TheoryData<Error, int, string> Errors => new()
    {
        { new DomainError("Invitation.Expired", "expired"), 422, "Unprocessable Content" },
        { new UnauthorizedError("Auth.Required", "login"), 401, "Unauthorized" },
        { new ForbiddenError("Auth.Forbidden", "nope"), 403, "Forbidden" },
        { new NotFoundError("Trainer.NotFound", "missing"), 404, "Not Found" },
        { new ConflictError("Invitation.PendingExists", "pending"), 409, "Conflict" },
    };

    [Theory]
    [MemberData(nameof(Errors))]
    public void Error_maps_to_problem_details(Error error, int status, string title)
    {
        var problem = error.ToProblemDetails();

        Assert.IsNotType<HttpValidationProblemDetails>(problem);
        Assert.Equal(status, problem.Status);
        Assert.Equal(title, problem.Title);
        Assert.Equal(error.Message, problem.Detail);
        Assert.Equal(error.Code, problem.Extensions[ResultHttpExtensions.CodeExtension]);
        Assert.Equal(status, error.ToStatusCode());
    }

    [Fact]
    public void Validation_error_maps_to_validation_problem_details_with_errors()
    {
        ValidationError error = new(
            new FieldError("email", "Email.Required", "required"),
            new FieldError("email", "Email.Invalid", "invalid"));

        HttpValidationProblemDetails problem = Assert.IsType<HttpValidationProblemDetails>(error.ToProblemDetails());

        Assert.Equal(400, problem.Status);
        Assert.Equal(ValidationError.ValidationCode, problem.Extensions[ResultHttpExtensions.CodeExtension]);
        Assert.Equal(["required", "invalid"], problem.Errors["email"]);
    }

    [Fact]
    public void ToProblem_wraps_problem_details()
    {
        ProblemHttpResult result = new NotFoundError("X", "y").ToProblem();

        Assert.Equal(404, result.StatusCode);
        Assert.Equal("X", result.ProblemDetails.Extensions[ResultHttpExtensions.CodeExtension]);
    }

    [Fact]
    public void ToHttpResult_uses_success_factory_for_success()
    {
        Results<Ok<int>, ProblemHttpResult> http = Result.Success(5).ToHttpResult(TypedResults.Ok);

        Assert.Equal(5, Assert.IsType<Ok<int>>(http.Result).Value);
    }

    [Fact]
    public async Task ToHttpResult_uses_problem_for_failure_on_async_receivers()
    {
        Results<Ok<int>, ProblemHttpResult> fromTask = await Task.FromResult(Result.Failure<int>(new ConflictError("X", "y"))).ToHttpResult(TypedResults.Ok);
        Results<Ok<int>, ProblemHttpResult> fromValueTask = await ValueTask.FromResult(Result.Failure<int>(new ForbiddenError("X", "y"))).ToHttpResult(TypedResults.Ok);

        Assert.Equal(409, Assert.IsType<ProblemHttpResult>(fromTask.Result).StatusCode);
        Assert.Equal(403, Assert.IsType<ProblemHttpResult>(fromValueTask.Result).StatusCode);
    }

    [Fact]
    public void Unexpected_error_hides_its_message()
    {
        var problem = new UnexpectedError("Database.Unavailable", "connection to 10.0.0.5 refused").ToProblemDetails();

        Assert.Equal(500, problem.Status);
        Assert.Equal("Internal Server Error", problem.Title);
        Assert.Equal("An unexpected error occurred.", problem.Detail);
        Assert.Equal("Database.Unavailable", problem.Extensions[ResultHttpExtensions.CodeExtension]);
    }

    [Fact]
    public async Task ToOk_and_ToNoContent_map_both_tracks()
    {
        Assert.Equal(7, Assert.IsType<Ok<int>>(Result.Success(7).ToOk().Result).Value);
        Assert.IsType<NoContent>(Result.Success().ToNoContent().Result);
        Assert.Equal(404, Assert.IsType<ProblemHttpResult>((await Task.FromResult(Result.Failure<int>(new NotFoundError("X", "y"))).ToOk()).Result).StatusCode);
        Assert.Equal(422, Assert.IsType<ProblemHttpResult>((await Task.FromResult(Result.Failure<Unit>(new DomainError("X", "y"))).ToNoContent()).Result).StatusCode);
    }
}
