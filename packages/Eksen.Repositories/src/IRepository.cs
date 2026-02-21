using System.Linq.Expressions;

namespace Eksen.Repositories;

public interface IRepository<TEntity>
    : IRepository<TEntity, DefaultFilterParameters<TEntity>, DefaultIncludeOptions<TEntity>, DefaultQueryOptions>
    where TEntity : class;

public interface IRepository<TEntity, in TFilterParameters>
    : IRepository<TEntity, TFilterParameters, DefaultIncludeOptions<TEntity>, DefaultQueryOptions>
    where TFilterParameters : BaseFilterParameters<TEntity>
    where TEntity : class;

public interface IRepository<TEntity, in TFilterParameters, in TIncludeOptions>
    : IRepository<TEntity, TFilterParameters, TIncludeOptions, DefaultQueryOptions>
    where TIncludeOptions : BaseIncludeOptions<TEntity>
    where TFilterParameters : BaseFilterParameters<TEntity>
    where TEntity : class;

public interface IRepository<TEntity, in TFilterParameters, in TIncludeOptions, in TQueryOptions>
    : IReadOnlyRepository<TEntity, TFilterParameters, TIncludeOptions, TQueryOptions>
    where TEntity : class
    where TQueryOptions : BaseQueryOptions
    where TIncludeOptions : BaseIncludeOptions<TEntity>
    where TFilterParameters : BaseFilterParameters<TEntity>
{
    Task InsertAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default);

    Task InsertManyAsync(IEnumerable<TEntity> entities, bool autoSave = false, CancellationToken cancellationToken = default);

    Task RemoveAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default);

    Task RemoveAsync(Expression<Func<TEntity, bool>> predicate, bool autoSave = false, CancellationToken cancellationToken = default);

    Task RemoveManyAsync(IEnumerable<TEntity> entities, bool autoSave = false, CancellationToken cancellationToken = default);

    Task UpdateAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default);

    Task UpdateManyAsync(IEnumerable<TEntity> entities, bool autoSave = false, CancellationToken cancellationToken = default);
}