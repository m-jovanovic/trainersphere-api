namespace TrainerSphere.IntegrationTests.Contracts;

/// <summary>
/// RFC 9457 problem details as the API sends them (section 3 of the HTTP contract).
/// </summary>
public sealed record Problem(
    string? Type,
    string? Title,
    int Status,
    string? Detail,
    string? Instance,
    string? Code,
    string? TraceId,
    Dictionary<string, string[]>? Errors);

public sealed record Paged<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);
