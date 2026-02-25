using Eksen.Repositories;
using Eksen.ValueObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace Eksen.EntityFrameworkCore;

public abstract class EfCoreRepository<
    TDbContext,
    TEntity,
    TFilterParameters,
    TFindParameters,
    TIncludeOptions,
    TQueryOptions,
    TPaginationParameters,
    TSortingParameters
>(TDbContext dbContext)
    : EfCoreReadOnlyRepository<
            TDbContext,
            TEntity,
            TFilterParameters,
            TFindParameters,
            TIncludeOptions,
            TQueryOptions,
            TPaginationParameters,
            TSortingParameters>(dbContext),
        IRepository<TEntity,
            TFilterParameters,
            TFindParameters,
            TIncludeOptions,
            TQueryOptions,
            TPaginationParameters,
            TSortingParameters>
    where TEntity : class, IEntity
    where TDbContext : DbContext
    where TQueryOptions : BaseQueryOptions, new()
    where TIncludeOptions : BaseIncludeOptions<TEntity>, new()
    where TFilterParameters : BaseFilterParameters<TEntity>, new()
    where TFindParameters : TFilterParameters, new()
    where TPaginationParameters : BasePaginationParameters, new()
    where TSortingParameters : BaseSortingParameters, new()
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
            AsNoTracking = false,
            IgnoreAutoIncludes = true
        };

        var entities = await GetListAsync(
            filterParameters,
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