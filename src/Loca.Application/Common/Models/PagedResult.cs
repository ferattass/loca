namespace Loca.Application.Common.Models;

/// <summary>
/// Sayfali liste yaniti. Sartnamede istenen alanlarin tamami burada.
/// </summary>
public sealed class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required int PageNumber { get; init; }
    public required int PageSize { get; init; }
    public required int TotalCount { get; init; }

    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    public static PagedResult<T> Empty(int pageNumber, int pageSize) => new()
    {
        Items = [],
        PageNumber = pageNumber,
        PageSize = pageSize,
        TotalCount = 0
    };
}

/// <summary>
/// Sayfalama parametreleri.
/// </summary>
public sealed record PaginationRequest
{
    /// <summary>Tek istekte donebilecek en fazla kayit. Hizmet reddi (DoS) korumasi.</summary>
    public const int MaxPageSize = 100;

    private const int DefaultPageSize = 20;

    private readonly int _pageNumber = 1;
    private readonly int _pageSize = DefaultPageSize;

    public int PageNumber
    {
        get => _pageNumber;
        init => _pageNumber = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = value < 1 ? DefaultPageSize : Math.Min(value, MaxPageSize);
    }

    public int Skip => (PageNumber - 1) * PageSize;
}
