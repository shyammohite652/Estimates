namespace ConstructionEstimator.Application.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed record PageRequest(int Page, int PageSize)
{
    public const int DefaultPageSize = 100;
    public const int MaxPageSize = 500;

    public static PageRequest Create(int? page, int? pageSize)
    {
        var p = page ?? 1;
        var s = pageSize ?? DefaultPageSize;
        if (p < 1) throw new RequestValidationException("page", "Page must be 1 or greater.");
        if (s is < 1 or > MaxPageSize)
            throw new RequestValidationException("pageSize", $"Page size must be between 1 and {MaxPageSize}.");
        return new PageRequest(p, s);
    }

    public int Skip => (Page - 1) * PageSize;
}
