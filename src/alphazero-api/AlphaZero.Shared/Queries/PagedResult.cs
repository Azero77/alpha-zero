namespace AlphaZero.Shared.Queries;

public record PagedResult<T>(
    IEnumerable<T> Items, 
    int TotalCount, 
    int CurrentPage, 
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => CurrentPage < TotalPages;
    public bool HasPreviousPage => CurrentPage > 1;
}