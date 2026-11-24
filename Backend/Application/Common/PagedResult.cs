namespace Application.Common;

/// <summary>
/// One page of a longer list. <paramref name="Page"/> starts at 1. A page past the end is not an
/// error: it has no items, and <paramref name="TotalCount"/> still tells how many there are.
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)(((long)TotalCount + PageSize - 1) / PageSize);

    public PagedResult<TResult> Map<TResult>(Func<T, TResult> map) =>
        new(Items.Select(map).ToList(), Page, PageSize, TotalCount);
}
