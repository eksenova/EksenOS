using System.Linq.Expressions;

namespace Eksen.Repositories;

public interface IReadOnlyRepository<TEntity>
    : IReadOnlyRepository<TEntity, DefaultFilterParameters<TEntity>, DefaultIncludeOptions<TEntity>, DefaultQueryOptions>
    where TEntity : class;

public interface IReadOnlyRepository<TEntity, in TFilterParameters>
    : IReadOnlyRepository<TEntity, TFilterParameters, DefaultIncludeOptions<TEntity>, DefaultQueryOptions>
    where TFilterParameters : BaseFilterParameters<TEntity>
    where TEntity : class;

public interface IReadOnlyRepository<TEntity, in TFilterParameters, in TIncludeOptions>
    : IReadOnlyRepository<TEntity, TFilterParameters, TIncludeOptions, DefaultQueryOptions>
    where TIncludeOptions : BaseIncludeOptions<TEntity>
    where TFilterParameters : BaseFilterParameters<TEntity>
    where TEntity : class;

public interface IReadOnlyRepository<TEntity, in TFilterParameters, in TIncludeOptions, in TQueryOptions>
    where TIncludeOptions : BaseIncludeOptions<TEntity>
    where TFilterParameters : BaseFilterParameters<TEntity>
    where TQueryOptions : BaseQueryOptions
    where TEntity : class
{
    Task<TEntity?> FindAsync(
        TFilterParameters? filterParameters = null,
        TIncludeOptions? includeOptions = null,
        TQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default);

    Task<TEntity> GetAsync(
        TFilterParameters? filterParameters = null,
        TIncludeOptions? includeOptions = null,
        TQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default);

    Task<ICollection<TEntity>> GetListAsync(
        TFilterParameters? filterParameters = null,
        TIncludeOptions? includeOptions = null,
        string? sorting = null,
        int? skipCount = null,
        int? maxResultCount = null,
        TQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default);

    Task<long> CountAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        TQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default);
}