using Eksen.Core;
using Eksen.Core.ErrorHandling;
using Eksen.Repositories;
using Eksen.ValueObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace Eksen.EntityFrameworkCore;

public abstract class EfCoreIdRepository<TDbContext, TEntity, TId, TIdValue>(
    TDbContext dbContext
) : EfCoreIdRepository<
    TDbContext,
    TEntity,
    TId,
    TIdValue,
    DefaultFilterParameters<TEntity>,
    DefaultIncludeOptions<TEntity>
>(dbContext)
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

public abstract class EfCoreIdRepository<TDbContext, TEntity, TId, TIdValue, TFilterParameters>(
    TDbContext dbContext
) : EfCoreIdRepository<
    TDbContext,
    TEntity,
    TId,
    TIdValue,
    TFilterParameters,
    DefaultIncludeOptions<TEntity>
>(dbContext)
    where TDbContext : EksenDbContext
    where TFilterParameters : BaseFilterParameters<TEntity>, new()
    where TEntity : class, IEntity<TId, TIdValue>
    where TId : IEntityId<TId, TIdValue>
    where TIdValue :
    IComparable,
    IComparable<TIdValue>,
    IEquatable<TIdValue>,
    ISpanFormattable,
    ISpanParsable<TIdValue>,
    IUtf8SpanFormattable;

public abstract class EfCoreIdRepository<TDbContext, TEntity, TId, TIdValue, TFilterParameters, TIncludeOptions>(
    TDbContext dbContext
) : EfCoreRepository<
    TDbContext,
    TEntity,
    TFilterParameters,
    TIncludeOptions,
    DefaultQueryOptions,
    DefaultPaginationParameters,
    DefaultSortingParameters<TEntity>
>(dbContext), IIdRepository<TEntity, TId, TIdValue, TFilterParameters, TIncludeOptions>
    where TDbContext : EksenDbContext
    where TFilterParameters : BaseFilterParameters<TEntity>, new()
    where TIncludeOptions : BaseIncludeOptions<TEntity>, new()
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
    public virtual async Task<TEntity> GetAsync(
        TId id,
        TFilterParameters? filterParameters = null,
        TIncludeOptions? includeOptions = null,
        DefaultQueryOptions? queryOptions = null,
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
        DefaultQueryOptions? queryOptions = null,
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
        return await queryable.FirstOrDefaultAsync(x => (object)x.Id == (object)id, cancellationToken)
               ?? throw CoreErrors.ObjectNotFound.Raise(typeof(TEntity), id);
    }
}

public abstract class EfCoreRepository<TDbContext, TEntity>(
    TDbContext dbContext
) : EfCoreRepository<TDbContext, TEntity, DefaultFilterParameters<TEntity>, DefaultIncludeOptions<TEntity>,
    DefaultQueryOptions, DefaultPaginationParameters,
    DefaultSortingParameters<TEntity>>(dbContext)
    where TEntity : class, IEntity
    where TDbContext : EksenDbContext;

public abstract class EfCoreRepository<TDbContext, TEntity, TFilterParameters>(
    TDbContext dbContext
) : EfCoreRepository<TDbContext, TEntity, TFilterParameters, DefaultIncludeOptions<TEntity>, DefaultQueryOptions,
    DefaultPaginationParameters,
    DefaultSortingParameters<TEntity>>(dbContext)
    where TEntity : class, IEntity
    where TDbContext : EksenDbContext
    where TFilterParameters : BaseFilterParameters<TEntity>, new();

public abstract class EfCoreRepository<TDbContext, TEntity, TFilterParameters, TIncludeOptions>(
    TDbContext dbContext
) : EfCoreRepository<TDbContext, TEntity, TFilterParameters, TIncludeOptions, DefaultQueryOptions,
    DefaultPaginationParameters,
    DefaultSortingParameters<TEntity>>(dbContext)
    where TEntity : class, IEntity
    where TDbContext : EksenDbContext
    where TIncludeOptions : BaseIncludeOptions<TEntity>, new()
    where TFilterParameters : BaseFilterParameters<TEntity>, new();

public abstract class EfCoreRepository<TDbContext, TEntity, TFilterParameters, TIncludeOptions, TQueryOptions>(
    TDbContext dbContext
) : EfCoreRepository<TDbContext, TEntity, TFilterParameters, TIncludeOptions, TQueryOptions,
    DefaultPaginationParameters,
    DefaultSortingParameters<TEntity>>(dbContext)
    where TEntity : class, IEntity
    where TDbContext : EksenDbContext
    where TQueryOptions : BaseQueryOptions, new()
    where TIncludeOptions : BaseIncludeOptions<TEntity>, new()
    where TFilterParameters : BaseFilterParameters<TEntity>, new();

public abstract class EfCoreRepository<TDbContext, TEntity, TFilterParameters, TIncludeOptions, TQueryOptions,
    TPaginationParameters>(
    TDbContext dbContext
) : EfCoreRepository<TDbContext, TEntity, TFilterParameters, TIncludeOptions, TQueryOptions, TPaginationParameters,
    DefaultSortingParameters<TEntity>>(dbContext)
    where TEntity : class, IEntity
    where TDbContext : EksenDbContext
    where TQueryOptions : BaseQueryOptions, new()
    where TIncludeOptions : BaseIncludeOptions<TEntity>, new()
    where TFilterParameters : BaseFilterParameters<TEntity>, new()
    where TPaginationParameters : BasePaginationParameters, new();

public abstract class EfCoreRepository<
    TDbContext,
    TEntity,
    TFilterParameters,
    TIncludeOptions,
    TQueryOptions,
    TPaginationParameters,
    TSortingParameters
>(TDbContext dbContext)
    : EfCoreReadOnlyRepository<
            TDbContext,
            TEntity,
            TFilterParameters,
            TIncludeOptions,
            TQueryOptions,
            TPaginationParameters,
            TSortingParameters>(dbContext),
        IRepository<TEntity,
            TFilterParameters,
            TIncludeOptions,
            TQueryOptions,
            TPaginationParameters,
            TSortingParameters>
    where TEntity : class, IEntity
    where TDbContext : EksenDbContext
    where TQueryOptions : BaseQueryOptions, new()
    where TIncludeOptions : BaseIncludeOptions<TEntity>, new()
    where TFilterParameters : BaseFilterParameters<TEntity>, new()
    where TPaginationParameters : BasePaginationParameters, new()
    where TSortingParameters : BaseSortingParameters<TEntity>, new()
{
    private readonly TDbContext _dbContext = dbContext;

    public virtual async Task InsertAsync(
        TEntity entity,
        bool autoSave = false,
        CancellationToken cancellationToken = default)
    {
        await GetDbSet().AddAsync(entity, cancellationToken);
        await SaveChangesAsync(autoSave, cancellationToken);
    }

    public virtual async Task InsertManyAsync(
        IEnumerable<TEntity> entities,
        bool autoSave = false,
        CancellationToken cancellationToken = default)
    {
        foreach (var entity in entities)
        {
            await InsertAsync(entity, autoSave: false, cancellationToken);
        }

        await SaveChangesAsync(autoSave, cancellationToken);
    }

    public virtual async Task UpdateAsync(
        TEntity entity,
        bool autoSave = false,
        CancellationToken cancellationToken = default)
    {
        GetDbSet().Update(entity);
        await SaveChangesAsync(autoSave, cancellationToken);
    }

    public virtual async Task UpdateManyAsync(
        IEnumerable<TEntity> entities,
        bool autoSave = false,
        CancellationToken cancellationToken = default)
    {
        foreach (var entity in entities)
        {
            await UpdateAsync(entity, autoSave: false, cancellationToken);
        }

        await SaveChangesAsync(autoSave, cancellationToken);
    }

    public virtual async Task RemoveAsync(
        TEntity entity,
        bool autoSave = false,
        CancellationToken cancellationToken = default)
    {
        var dbSet = _dbContext.Set<TEntity>();

        var entry = _dbContext.Entry(entity);
        if (entry.State == EntityState.Detached)
        {
            dbSet.Attach(entity);
        }

        dbSet.Remove(entity);
        await SaveChangesAsync(autoSave, cancellationToken);
    }

    public virtual async Task RemoveAsync(
        TFilterParameters filterParameters,
        bool autoSave = false,
        CancellationToken cancellationToken = default)
    {
        var queryOptions = new TQueryOptions
        {
            AsNoTracking = false
        };

        var includeOptions = new TIncludeOptions
        {
            IgnoreAutoIncludes = true
        };

        var entities = await GetListAsync(
            filterParameters,
            includeOptions,
            queryOptions: queryOptions,
            cancellationToken: cancellationToken);

        await RemoveManyAsync(entities, autoSave, cancellationToken);
    }

    public virtual async Task RemoveManyAsync(
        IEnumerable<TEntity> entities,
        bool autoSave = false,
        CancellationToken cancellationToken = default)
    {
        foreach (var entity in entities)
        {
            await RemoveAsync(entity, autoSave: false, cancellationToken);
        }

        await SaveChangesAsync(autoSave, cancellationToken);
    }

    protected virtual async Task SaveChangesAsync(bool doSave, CancellationToken cancellationToken = default)
    {
        if (doSave)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}