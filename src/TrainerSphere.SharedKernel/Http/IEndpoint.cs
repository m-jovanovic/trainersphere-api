using Microsoft.AspNetCore.Routing;

namespace TrainerSphere.SharedKernel.Http;

/// <summary>
/// One HTTP endpoint of a feature slice; discovered and mapped at startup.
/// </summary>
public interface IEndpoint
{
    void MapEndpoint(IEndpointRouteBuilder app);
}
