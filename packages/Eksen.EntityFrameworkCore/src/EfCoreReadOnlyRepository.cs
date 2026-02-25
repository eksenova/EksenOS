using System.Linq.Dynamic.Core;
using Eksen.Core;
using Eksen.Core.ErrorHandling;
using Eksen.Entities;
using Eksen.Repositories;
using Eksen.ValueObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace Eksen.EntityFrameworkCore;

public class EfCoreReadOnlyIdRepository<TDbContext, TEntity, TId, TIdValue>(
    TDbContext dbContext
) : EfCoreReadOnlyRepository<
    TDbContext,
    TEntity,
    DefaultFilterParameters<TEntity>,
    DefaultIdFindParameters<TId, TIdValue>,
    DefaultIncludeOptions<TEntity>,
    DefaultQueryOptions,
    DefaultPaginationParameters,
    DefaultSortingParameters
>(dbContext), IReadOnlyIdRepository<TEntity, TId, TIdValue>
    where TDbContext : DbContext
    where TEntity : class, IEntity<TId, TIdValue>
    where TId : IEntityId<TId, TIdValue>
    where TIdValue :
    IComparable,
    IComparable<TIdValue>,
    IEquatable<TIdValue>,
    ISpanFormattable,
    ISpanParsable<TIdValue>,
    IUtf8SpanFormattable
{
    protected override async Task<TEntity> GetEntityAsync(
        IQueryable<TEntity> queryable,
        DefaultIdFindParameters<TId, TIdValue> findParameters,
        CancellationToken cancellationToken = default)
    {
        var id = findParameters.Id;

        return await queryable.FirstOrDefaultAsync(x => (object)x.Id == (object)id, cancellationToken)
               ?? throw CoreErrors.ObjectNotFound.Raise(typeof(TEntity), id);
    }
}

public abstract class EfCoreReadOnlyRepository<TDbContext, TEntity, TFindParameters>(
    TDbContext dbContext
) : EfCoreReadOnlyRepository<TDbContext, TEntity, DefaultFilterParameters<TEntity>, TFindParameters, DefaultIncludeOptions<TEntity>,
    DefaultQueryOptions, DefaultPaginationParameters,
    DefaultSortingParameters>(dbContext)
    where TEntity : class, IEntity
    where TDbContext : DbContext
    where TFindParameters : DefaultFilterParameters<TEntity>, new();

public abstract class EfCoreReadOnlyRepository<TDbContext, TEntity, TFilterParameters, TFindParameters>(
    TDbContext dbContext
) : EfCoreReadOnlyRepository<TDbContext, TEntity, TFilterParameters, TFindParameters, DefaultIncludeOptions<TEntity>, DefaultQueryOptions,
    DefaultPaginationParameters,
    DefaultSortingParameters>(dbContext)
    where TEntity : class, IEntity
    where TDbContext : DbContext
    where TFilterParameters : BaseFilterParameters<TEntity>, new()
    where TFindParameters : TFilterParameters;

public abstract class EfCoreReadOnlyRepository<TDbContext, TEntity, TFilterParameters, TFindParameters, TIncludeOptions>(
    TDbContext dbContext
) : EfCoreReadOnlyRepository<TDbContext, TEntity, TFilterParameters, TFindParameters, TIncludeOptions, DefaultQueryOptions,
    DefaultPaginationParameters,
    DefaultSortingParameters>(dbContext)
    where TEntity : class, IEntity
    where TDbContext : DbContext
    where TIncludeOptions : BaseIncludeOptions<TEntity>, new()
    where TFilterParameters : BaseFilterParameters<TEntity>, new()
    where TFindParameters : TFilterParameters;

public abstract class EfCoreReadOnlyRepository<TDbContext, TEntity, TFilterParameters, TFindParameters, TIncludeOptions, TQueryOptions>(
    TDbContext dbContext
) : EfCoreReadOnlyRepository<TDbContext, TEntity, TFilterParameters, TFindParameters, TIncludeOptions, TQueryOptions,
    DefaultPaginationParameters,
    DefaultSortingParameters>(dbContext)
    where TEntity : class, IEntity
    where TDbContext : DbContext
    where TQueryOptions : BaseQueryOptions, new()
    where TIncludeOptions : BaseIncludeOptions<TEntity>, new()
    where TFilterParameters : BaseFilterParameters<TEntity>, new()
    where TFindParameters : TFilterParameters;

public abstract class EfCoreReadOnlyRepository<TDbContext, TEntity, TFilterParameters, TFindParameters, TIncludeOptions, TQueryOptions,
    TPaginationParameters>(
    TDbContext dbContext
) : EfCoreReadOnlyRepository<TDbContext, TEntity, TFilterParameters, TFindParameters, TIncludeOptions, TQueryOptions, TPaginationParameters,
    DefaultSortingParameters>(dbContext)
    where TEntity : class, IEntity
    where TDbContext : DbContext
    where TQueryOptions : BaseQueryOptions, new()
    where TIncludeOptions : BaseIncludeOptions<TEntity>, new()
    where TFilterParameters : BaseFilterParameters<TEntity>, new()
    where TFindParameters : TFilterParameters
    where TPaginationParameters : BasePaginationParameters, new();

public abstract class EfCoreReadOnlyRepository<TDbContext, TEntity, TFilterParameters, TFindParameters, TIncludeOptions, TQueryOptions,
    TPaginationParameters, TSortingParameters>(
    TDbContext dbContext
) : IReadOnlyRepository<TEntity, TFilterParameters, TFindParameters, TIncludeOptions, TQueryOptions, TPaginationParameters,
    TSortingParameters>
    where TEntity : class, IEntity
    where TDbContext : DbContext
    where TQueryOptions : BaseQueryOptions, new()
    where TIncludeOptions : BaseIncludeOptions<TEntity>, new()
    where TFilterParameters : BaseFilterParameters<TEntity>, new()
    where TFindParameters : class
    where TPaginationParameters : BasePaginationParameters, new()
    where TSortingParameters : BaseSortingParameters, new()
{
    public virtual async Task<TEntity> GetAsync(
        TFindParameters findParameters,
        TIncludeOptions? includeOptions = null,
        TQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default)
    {
        var queryable = GetQueryable(
            queryOptions
        );

        queryable = ApplyIncludes(
            queryable,
            includeOptions
        );

        if (findParameters is TFilterParameters filterParameters)
        {
            queryable = ApplyQueryFilters(
                queryable,
                filterParameters
            );
        }

        var entity = await GetEntityAsync(
            queryable,
            findParameters,
            cancellationToken
        );

        return entity;
    }

    public virtual async Task<TEntity?> FindAsync(
        TFindParameters findParameters,
        TIncludeOptions? includeOptions = null,
        TQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default)
    {
        TEntity? entity;

        try
        {
            entity = await GetAsync(
                findParameters,
                includeOptions,
                queryOptions,
                cancellationToken
            );
        }
        catch (EksenException exception)
        {
            if (exception.ErrorDescriptor.ErrorType != ErrorType.NotFound)
            {
                throw;
            }

            entity = null;
        }

        return entity;
    }

    public virtual async Task<ICollection<TEntity>> GetListAsync(
        TFilterParameters? filterParameters = null,
        TIncludeOptions? includeOptions = null,
        TPaginationParameters? paginationParameters = null,
        TSortingParameters? sortingParameters = null,
        TQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default)
    {
        var queryable = GetQueryable(queryOptions);

        queryable = ApplyIncludes(
            queryable,
            includeOptions
        );

        queryable = ApplyQueryFilters(
            queryable,
            filterParameters
        );

        queryable = ApplySorting(queryable, sortingParameters);

        queryable = ApplyPaging(queryable, paginationParameters);

        return await queryable.ToListAsync(cancellationToken);
    }

    public virtual Task<long> CountAsync(
        TFilterParameters? filterParameters = null,
        TQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default)
    {
        var queryable = GetQueryable(queryOptions);

        queryable = ApplyQueryFilters(
            queryable,
            filterParameters
        );

        return queryable.LongCountAsync(cancellationToken);
    }

    protected virtual IQueryable<TEntity> ApplyQueryFilters(
        IQueryable<TEntity> queryable,
        TFilterParameters? filterParameters = null)
    {
        if (filterParameters is DefaultFilterParameters<TEntity> defaultFilterParameters)
        {
            var predicate = defaultFilterParameters.Predicate;

            queryable = predicate != null
                ? queryable.Where(predicate)
                : queryable;
        }

        if (filterParameters == null)
        {
            return ApplyDefaultFilters(queryable);
        }

        return queryable;
    }

    protected virtual IQueryable<TEntity> ApplyDefaultFilters(IQueryable<TEntity> queryable)
    {
        return queryable;
    }

    protected virtual IQueryable<TEntity> ApplyIncludes(
        IQueryable<TEntity> queryable,
        TIncludeOptions? includeOptions = null)
    {
        if (includeOptions is DefaultIncludeOptions<TEntity> defaultIncludeOptions)
        {
            var includes = defaultIncludeOptions.Includes;

            if (includes is { Count: > 0 })
            {
                queryable = includes
                    .Aggregate(queryable,
                        (current, include)
                            => current.Include(include));
            }
        }

        if (includeOptions == null)
        {
            return ApplyDefaultIncludes(queryable);
        }

        return queryable;
    }

    protected virtual IQueryable<TEntity> ApplyDefaultIncludes(IQueryable<TEntity> queryable)
    {
        return queryable;
    }

    protected virtual IQueryable<TEntity> ApplyQueryOptions(
        IQueryable<TEntity> queryable,
        TQueryOptions? queryOptions = null)
    {
        if (queryOptions != null)
        {
            if (queryOptions.IgnoreQueryFilters)
            {
                queryable = queryable
                    .IgnoreQueryFilters();
            }

            if (queryOptions.IgnoreAutoIncludes)
            {
                queryable = queryable
                    .IgnoreAutoIncludes();
            }

            if (queryOptions.AsNoTracking)
            {
                queryable = queryable
                    .AsNoTracking();
            }
        }

        return queryable;
    }

    protected virtual IQueryable<TEntity> ApplySorting(
        IQueryable<TEntity> queryable,
        TSortingParameters? sortingParameters = null)
    {
        if (sortingParameters == null)
        {
            return ApplyDefaultSorting(queryable);
        }

        if (sortingParameters is DefaultSortingParameters defaultSortingParameters)
        {
            var sorting = defaultSortingParameters.Sorting;

            queryable = !string.IsNullOrWhiteSpace(sorting)
                ? queryable.OrderBy(sorting)
                : typeof(IHasCreationTime).IsAssignableFrom(typeof(TEntity))
                    ? queryable.OrderByDescending(x => EF.Property<DateTime>(x, "CreationTime"))
                    : queryable.OrderByDescending(x => EF.Property<int>(x, "Id"));
        }

        return queryable;
    }

    protected virtual IQueryable<TEntity> ApplyDefaultSorting(IQueryable<TEntity> queryable)
    {
        if (typeof(IHasCreationTime).IsAssignableFrom(typeof(TEntity)))
        {
            return queryable.OrderBy($"{nameof(IHasCreationTime.CreationTime)} DESC");
        }

        return queryable;
    }

    protected virtual IQueryable<TEntity> ApplyPaging(
        IQueryable<TEntity> queryable,
        TPaginationParameters? paginationParameters = null)
    {
        if (paginationParameters == null)
        {
            return ApplyDefaultPaging(queryable);
        }

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

    protected virtual IQueryable<TEntity> ApplyDefaultPaging(IQueryable<TEntity> queryable)
    {
        return queryable;
    }

    protected abstract Task<TEntity> GetEntityAsync(
        IQueryable<TEntity> queryable,
        TFindParameters findParameters,
        CancellationToken cancellationToken = default
    );

    protected virtual IQueryable<TEntity> GetQueryable(TQueryOptions? queryOptions = null)
    {
        return ApplyQueryOptions(GetDbSet(), queryOptions);
    }

    protected virtual TDbContext GetDbContext()
    {
        return dbContext;
    }

    protected virtual DbSet<TEntity> GetDbSet()
    {
        return GetDbContext().Set<TEntity>();
    }
}