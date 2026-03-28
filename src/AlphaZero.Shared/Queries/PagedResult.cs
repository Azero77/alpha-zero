namespace AlphaZero.Shared.Queries;

public record PagedResult<T>(
    IEnumerable<T> Items, 
    int TotalCount, 
    int Page, 
    int PerPage)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PerPage);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}