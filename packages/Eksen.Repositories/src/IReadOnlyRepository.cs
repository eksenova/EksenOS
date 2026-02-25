using Eksen.ValueObjects.Entities;

namespace Eksen.Repositories;

public interface IReadOnlyIdRepository<TEntity, TId, TIdValue>
    : IReadOnlyRepository<TEntity,
        DefaultFilterParameters<TEntity>,
        DefaultIdFindParameters<TId, TIdValue>,
        DefaultIncludeOptions<TEntity>,
        DefaultQueryOptions,
        DefaultPaginationParameters
    >
    where TEntity : class, IEntity<TId, TIdValue>
    where TId : IEntityId<TId, TIdValue>
    where TIdValue :
    IComparable<TIdValue>,
    IComparable,
    IEquatable<TIdValue>,
    ISpanFormattable,
    ISpanParsable<TIdValue>,
    IUtf8SpanFormattable;

public interface IReadOnlyRepository<TEntity, in TFindParameters>
    : IReadOnlyRepository<
        TEntity,
        DefaultFilterParameters<TEntity>,
        TFindParameters,
        DefaultIncludeOptions<TEntity>,
        DefaultQueryOptions,
        DefaultPaginationParameters
    >
    where TFindParameters : class
    where TEntity : class, IEntity;

public interface IReadOnlyRepository<TEntity, in TFilterParameters, in TFindParameters>
    : IReadOnlyRepository<
        TEntity,
        TFilterParameters,
        TFindParameters,
        DefaultIncludeOptions<TEntity>,
        DefaultQueryOptions,
        DefaultPaginationParameters
    >
    where TFilterParameters : BaseFilterParameters<TEntity>, new()
    where TFindParameters : class
    where TEntity : class, IEntity;

public interface IReadOnlyRepository<TEntity, in TFilterParameters, in TFindParameters, in TIncludeOptions>
    : IReadOnlyRepository<
        TEntity,
        TFilterParameters,
        TFindParameters,
        TIncludeOptions,
        DefaultQueryOptions,
        DefaultPaginationParameters
    >
    where TIncludeOptions : BaseIncludeOptions<TEntity>, new()
    where TFilterParameters : BaseFilterParameters<TEntity>, new()
    where TFindParameters : class
    where TEntity : class, IEntity;

public interface IReadOnlyRepository<
    TEntity,
    in TFilterParameters,
    in TFindParameters,
    in TIncludeOptions,
    in TQueryOptions
>
    : IReadOnlyRepository<
        TEntity,
        TFilterParameters,
        TFindParameters,
        TIncludeOptions,
        TQueryOptions,
        DefaultPaginationParameters
    >
    where TIncludeOptions : BaseIncludeOptions<TEntity>, new()
    where TFilterParameters : BaseFilterParameters<TEntity>, new()
    where TFindParameters : class
    where TQueryOptions : BaseQueryOptions, new()
    where TEntity : class, IEntity;

public interface IReadOnlyRepository<
    TEntity,
    in TFilterParameters,
    in TFindParameters,
    in TIncludeOptions,
    in TQueryOptions,
    in TPaginationParameters
>
    : IReadOnlyRepository<
        TEntity,
        TFilterParameters,
        TFindParameters,
        TIncludeOptions,
        TQueryOptions,
        TPaginationParameters,
        DefaultSortingParameters
    >
    where TIncludeOptions : BaseIncludeOptions<TEntity>, new()
    where TFilterParameters : BaseFilterParameters<TEntity>, new()
    where TFindParameters : class
    where TQueryOptions : BaseQueryOptions, new()
    where TPaginationParameters : BasePaginationParameters, new()
    where TEntity : class, IEntity;

public interface IReadOnlyRepository<
    TEntity,
    in TFilterParameters,
    in TFindParameters,
    in TIncludeOptions,
    in TQueryOptions,
    in TPaginationParameters,
    in TSortingParameters
>
    where TIncludeOptions : BaseIncludeOptions<TEntity>, new()
    where TFilterParameters : BaseFilterParameters<TEntity>, new()
    where TFindParameters : class
    where TQueryOptions : BaseQueryOptions, new()
    where TPaginationParameters : BasePaginationParameters, new()
    where TSortingParameters : BaseSortingParameters, new()
    where TEntity : class, IEntity
{
    Task<TEntity?> FindAsync(
        TFindParameters filterParameters,
        TIncludeOptions? includeOptions = null,
        TQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default);

    Task<TEntity> GetAsync(
        TFindParameters filterParameters,
        TIncludeOptions? includeOptions = null,
        TQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default);

    Task<ICollection<TEntity>> GetListAsync(
        TFilterParameters? filterParameters = null,
        TIncludeOptions? includeOptions = null,
        TPaginationParameters? paginationParameters = null,
        TSortingParameters? sortingParameters = null,
        TQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default);

    Task<long> CountAsync(
        TFilterParameters? filterParameters = null,
        TQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default);
}