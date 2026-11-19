namespace TrainerSphere.SharedKernel.Http;

public sealed record Paged<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed record PageRequest(int Page, int PageSize)
{
    public const int DefaultPageSize = 20;

    public int Offset => (Page - 1) * PageSize;

    public static PageRequest From(int? page, int? pageSize) => new(page ?? 1, pageSize ?? DefaultPageSize);
}
