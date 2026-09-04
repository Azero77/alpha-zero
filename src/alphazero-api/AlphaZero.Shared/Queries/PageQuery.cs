using System.Linq.Expressions;

namespace AlphaZero.Shared.Queries
{
    public interface IPagedQuery
    {
        /// <summary>
        /// Page number. If null then default is 1.
        /// </summary>
        int? Page { get; }

        /// <summary>
        /// Records number per page (page size).
        /// </summary>
        int? PerPage { get; }
    }

    public struct PageData
    {
        public int Offset { get; }

        public int Next { get; }

        public PageData(int offset, int next)
        {
            this.Offset = offset;
            this.Next = next;
        }
    }

    public static class PagedQueryHelper
    {
        public const string Offset = "Offset";

        public const string Next = "Next";

        public static PageData GetPageData(IPagedQuery query)
        {
            int offset;
            if (!query.Page.HasValue ||
                !query.PerPage.HasValue)
            {
                offset = 0;
            }
            else
            {
                offset = (query.Page.Value - 1) * query.PerPage.Value;
            }

            int next;
            if (!query.PerPage.HasValue)
            {
                next = int.MaxValue;
            }
            else
            {
                next = query.PerPage.Value;
            }

            return new PageData(offset, next);
        }

        public static IQueryable<T> AppendPageStatement<T,TKey>(IQueryable<T> query,
            PageData pagingData,
            Expression<Func<T, TKey>>? orderBy = null)
        {
            query = query.Skip(pagingData.Offset)
                .Take(pagingData.Next);

            if(orderBy is not null)
                query = query.OrderBy(orderBy);

            return query;
        }
    }
}