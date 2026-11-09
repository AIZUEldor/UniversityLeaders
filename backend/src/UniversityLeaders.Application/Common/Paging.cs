namespace UniversityLeaders.Application.Common;

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

/// <summary>Validated paging window shared by every list endpoint.</summary>
public readonly record struct Paging(int Page, int PageSize)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public int Skip => (Page - 1) * PageSize;

    public static Paging From(int? page, int? pageSize)
    {
        int number = page ?? 1;
        int size = pageSize ?? DefaultPageSize;
        if (number < 1 || size < 1 || size > MaxPageSize)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["page"] = new[] { $"page >= 1 and pageSize 1-{MaxPageSize}." },
            });
        }

        return new Paging(number, size);
    }

    public PagedResponse<T> Result<T>(IReadOnlyList<T> items, int total)
    {
        return new PagedResponse<T>(items, total, Page, PageSize);
    }
}
