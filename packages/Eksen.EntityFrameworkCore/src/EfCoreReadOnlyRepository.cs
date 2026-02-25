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
) : EfCoreReadOnlyIdRepository<
    TDbContext,
    TEntity,
    TId,
    TIdValue,
    DefaultFilterParameters<TEntity>,
    DefaultIncludeOptions<TEntity>,
    DefaultQueryOptions,
    DefaultPaginationParameters,
    DefaultSortingParameters<TEntity>
>(dbContext), IReadOnlyIdRepository<TEntity, TId, TIdValue>
    where TDbContext : EksenDbContext
    where TEntity : class, IEntity<TId, TIdValue>
    where TId : IEntityId<TId, TIdValue>
    where TIdValue :
    IComparable,
    IComparable<TIdValue>,
    IEquatable<TIdValue>,
    ISpanFormattable,
    ISpanParsable<TIdValue>,
    IUtf8SpanFormattable;

public abstract class EfCoreReadOnlyRepository<TDbContext, TEntity>(
    TDbContext dbContext
) : EfCoreReadOnlyRepository<TDbContext, TEntity, DefaultFilterParameters<TEntity>, DefaultIncludeOptions<TEntity>,
    DefaultQueryOptions, DefaultPaginationParameters,
    DefaultSortingParameters<TEntity>>(dbContext)
    where TEntity : class, IEntity
    where TDbContext : EksenDbContext;

public abstract class EfCoreReadOnlyRepository<TDbContext, TEntity, TFilterParameters>(
    TDbContext dbContext
) : EfCoreReadOnlyRepository<TDbContext, TEntity, TFilterParameters, DefaultIncludeOptions<TEntity>, DefaultQueryOptions,
    DefaultPaginationParameters,
    DefaultSortingParameters<TEntity>>(dbContext)
    where TEntity : class, IEntity
    where TDbContext : EksenDbContext
    where TFilterParameters : BaseFilterParameters<TEntity>, new();

public abstract class EfCoreReadOnlyRepository<TDbContext, TEntity, TFilterParameters, TIncludeOptions>(
    TDbContext dbContext
) : EfCoreReadOnlyRepository<TDbContext, TEntity, TFilterParameters, TIncludeOptions, DefaultQueryOptions,
    DefaultPaginationParameters,
    DefaultSortingParameters<TEntity>>(dbContext)
    where TEntity : class, IEntity
    where TDbContext : EksenDbContext
    where TIncludeOptions : BaseIncludeOptions<TEntity>, new()
    where TFilterParameters : BaseFilterParameters<TEntity>, new();

public abstract class EfCoreReadOnlyRepository<TDbContext, TEntity, TFilterParameters, TIncludeOptions, TQueryOptions>(
    TDbContext dbContext
) : EfCoreReadOnlyRepository<TDbContext, TEntity, TFilterParameters, TIncludeOptions, TQueryOptions,
    DefaultPaginationParameters,
    DefaultSortingParameters<TEntity>>(dbContext)
    where TEntity : class, IEntity
    where TDbContext : EksenDbContext
    where TQueryOptions : BaseQueryOptions, new()
    where TIncludeOptions : BaseIncludeOptions<TEntity>, new()
    where TFilterParameters : BaseFilterParameters<TEntity>, new();

public abstract class EfCoreReadOnlyRepository<TDbContext, TEntity, TFilterParameters, TIncludeOptions, TQueryOptions,
    TPaginationParameters>(
    TDbContext dbContext
) : EfCoreReadOnlyRepository<TDbContext, TEntity, TFilterParameters, TIncludeOptions, TQueryOptions, TPaginationParameters,
    DefaultSortingParameters<TEntity>>(dbContext)
    where TEntity : class, IEntity
    where TDbContext : EksenDbContext
    where TQueryOptions : BaseQueryOptions, new()
    where TIncludeOptions : BaseIncludeOptions<TEntity>, new()
    where TFilterParameters : BaseFilterParameters<TEntity>, new()
    where TPaginationParameters : BasePaginationParameters, new();

public abstract class EfCoreReadOnlyRepository<TDbContext, TEntity, TFilterParameters, TIncludeOptions, TQueryOptions,
    TPaginationParameters, TSortingParameters>(
    TDbContext dbContext
) : IReadOnlyRepository<TEntity, TFilterParameters, TIncludeOptions, TQueryOptions, TPaginationParameters,
    TSortingParameters>
    where TEntity : class, IEntity
    where TDbContext : EksenDbContext
    where TQueryOptions : BaseQueryOptions, new()
    where TIncludeOptions : BaseIncludeOptions<TEntity>, new()
    where TFilterParameters : BaseFilterParameters<TEntity>, new()
    where TPaginationParameters : BasePaginationParameters, new()
    where TSortingParameters : BaseSortingParameters<TEntity>, new()
{
    public virtual async Task<TEntity> GetAsync(
        TFilterParameters filterParameters,
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

        var entity = await GetSingleAsync(
            queryable,
            filterParameters,
            cancellationToken
        );

        return entity;
    }

    public virtual async Task<TEntity?> FindAsync(
        TFilterParameters filterParameters,
        TIncludeOptions? includeOptions = null,
        TQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default)
    {
        TEntity? entity;

        try
        {
            entity = await GetAsync(
                filterParameters,
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
        switch (includeOptions)
        {
            case null:
                return ApplyDefaultIncludes(queryable);

            case DefaultIncludeOptions<TEntity> defaultIncludeOptions:
            {
                var includes = defaultIncludeOptions.Includes;

                if (includes is { Count: > 0 })
                {
                    queryable = includes
                        .Aggregate(queryable,
                            (current, include)
                                => current.Include(include));
                }

                break;
            }
        }

        if (includeOptions.IgnoreAutoIncludes)
        {
            queryable = queryable
                .IgnoreAutoIncludes();
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

        if (sortingParameters is DefaultSortingParameters<TEntity> defaultSortingParameters)
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

    protected async Task<TEntity> GetSingleAsync(
        IQueryable<TEntity> queryable,
        TFilterParameters? filterParameters = null,
        CancellationToken cancellationToken = default
    )
    {
        if (filterParameters != null)
        {
            queryable = ApplyQueryFilters(
                queryable,
                filterParameters
            );
        }

        var entity = await queryable.SingleOrDefaultAsync(cancellationToken);

        if (entity == null)
        {
            throw CoreErrors.ObjectNotFound.Raise(typeof(TEntity), filterParameters);
        }

        return entity;
    }

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

public abstract class EfCoreReadOnlyIdRepository<TDbContext, TEntity, TId, TIdValue, TFilterParameters, TIncludeOptions, TQueryOptions,
    TPaginationParameters, TSortingParameters>(
    TDbContext dbContext
) : EfCoreReadOnlyRepository<
        TDbContext,
        TEntity,
        TFilterParameters,
        TIncludeOptions,
        TQueryOptions,
        TPaginationParameters,
        TSortingParameters
    >(dbContext),
    IReadOnlyIdRepository<
        TEntity,
        TId,
        TIdValue,
        TFilterParameters,
        TIncludeOptions,
        TQueryOptions,
        TPaginationParameters,
        TSortingParameters
    >
    where TEntity : class, IEntity<TId, TIdValue>
    where TDbContext : EksenDbContext
    where TQueryOptions : BaseQueryOptions, new()
    where TIncludeOptions : BaseIncludeOptions<TEntity>, new()
    where TFilterParameters : BaseFilterParameters<TEntity>, new()
    where TPaginationParameters : BasePaginationParameters, new()
    where TSortingParameters : BaseSortingParameters<TEntity>, new()
    where TId : IEntityId<TId, TIdValue>
    where TIdValue :
    IComparable,
    IComparable<TIdValue>,
    IEquatable<TIdValue>,
    ISpanFormattable,
    ISpanParsable<TIdValue>,
    IUtf8SpanFormattable
{
    public virtual async Task<TEntity> GetAsync(
        TId id,
        TFilterParameters? filterParameters = null,
        TIncludeOptions? includeOptions = null,
        TQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default)
    {
        var queryable = GetQueryable(
            queryOptions
        );

        queryable = ApplyQueryFilters(
            queryable,
            filterParameters
        );

        queryable = ApplyIncludes(
            queryable,
            includeOptions
        );

        var entity = await GetByIdAsync(
            queryable,
            id,
            cancellationToken
        );

        return entity;
    }

    public virtual async Task<TEntity?> FindAsync(
        TId id,
        TFilterParameters? filterParameters = null,
        TIncludeOptions? includeOptions = null,
        TQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default)
    {
        TEntity? entity;

        try
        {
            entity = await GetAsync(
                id,
                filterParameters,
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

    protected virtual async Task<TEntity> GetByIdAsync(
        IQueryable<TEntity> queryable,
        TId id,
        CancellationToken cancellationToken = default)
    {
        return await queryable.SingleOrDefaultAsync(x => (object)x.Id == (object)id, cancellationToken)
               ?? throw CoreErrors.ObjectNotFound.Raise(typeof(TEntity), id);
    }
}