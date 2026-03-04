using System.Linq.Dynamic.Core;
using Eksen.Entities;
using Eksen.Repositories;
using Eksen.ValueObjects.Entities;

#pragma warning disable IDE0130

// ReSharper disable once CheckNamespace
namespace Microsoft.EntityFrameworkCore;

public static class QueryableExtensions
{
    extension<TEntity>(IQueryable<TEntity> queryable)
        where TEntity : class, IEntity
    {
        public IQueryable<TEntity> Include(BaseIncludeOptions<TEntity> includeOptions)
        {
            queryable = includeOptions.IgnoreAutoIncludes
                ? queryable.IgnoreAutoIncludes()
                : queryable;

            queryable = includeOptions.Includes != null
                ? includeOptions.Includes
                    .Aggregate(queryable,
                        (current, include)
                            => current.Include(include))
                : queryable;

            return queryable;
        }

        public IQueryable<TEntity> OrderBy(BaseSortingParameters<TEntity> sortingParameters)
        {
            return !string.IsNullOrWhiteSpace(sortingParameters.Sorting)
                ? queryable.OrderBy(sortingParameters.Sorting)
                : queryable.OrderByDefault();
        }

        public IQueryable<TEntity> OrderByDefault()
        {
            queryable = typeof(IHasCreationTime).IsAssignableFrom(typeof(TEntity))
                ? queryable.OrderByDescending(x => EF.Property<DateTime>(x, "CreationTime"))
                : queryable.OrderByDescending(x => EF.Property<int>(x, "Id"));
            return queryable;
        }

        public IQueryable<TEntity> Where(BaseFilterParameters<TEntity> filterParameters)
        {
            queryable = filterParameters.Predicate != null
                ? queryable.Where(filterParameters.Predicate)
                : queryable;

            return queryable;
        }

        public IQueryable<TEntity> Page(BasePaginationParameters paginationParameters)
        {
            var skipCount = paginationParameters.SkipCount;
            var maxResultCount = paginationParameters.MaxResultCount;

            if (skipCount.HasValue)
            {
                queryable = queryable
                    .Skip(skipCount.Value);
            }

            if (maxResultCount.HasValue)
            {
                queryable = queryable
                    .Take(maxResultCount.Value);
            }

            return queryable;
        }

        public IQueryable<TEntity> WithOptions(BaseQueryOptions queryOptions)
        {
            if (queryOptions.IgnoreQueryFilters)
            {
                queryable = queryable
                    .IgnoreQueryFilters();
            }

            if (queryOptions.AsNoTracking)
            {
                queryable = queryable
                    .AsNoTracking();
            }

            return queryable;
        }
    }
}
