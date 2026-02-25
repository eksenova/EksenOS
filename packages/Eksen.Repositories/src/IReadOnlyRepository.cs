using Eksen.ValueObjects.Entities;

namespace Eksen.Repositories;

public interface IReadOnlyIdRepository<TEntity, in TId, in TIdValue>
    : IReadOnlyIdRepository<
        TEntity,
        TId,
        TIdValue,
        DefaultFilterParameters<TEntity>,
        DefaultIncludeOptions<TEntity>,
        DefaultQueryOptions,
        DefaultPaginationParameters,
        DefaultSortingParameters<TEntity>
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

public interface IReadOnlyIdRepository<TEntity, in TId, in TIdValue, in TFilterParameters>
    : IReadOnlyIdRepository<
        TEntity,
        TId,
        TIdValue,
        TFilterParameters,
        DefaultIncludeOptions<TEntity>,
        DefaultQueryOptions,
        DefaultPaginationParameters,
        DefaultSortingParameters<TEntity>
    >
    where TEntity : class, IEntity<TId, TIdValue>
    where TId : IEntityId<TId, TIdValue>
    where TIdValue :
    IComparable<TIdValue>,
    IComparable,
    IEquatable<TIdValue>,
    ISpanFormattable,
    ISpanParsable<TIdValue>,
    IUtf8SpanFormattable
    where TFilterParameters : BaseFilterParameters<TEntity>, new();

public interface IReadOnlyIdRepository<TEntity, in TId, in TIdValue, in TFilterParameters, in TIncludeOptions>
    : IReadOnlyIdRepository<
        TEntity,
        TId,
        TIdValue,
        TFilterParameters,
        TIncludeOptions,
        DefaultQueryOptions,
        DefaultPaginationParameters,
        DefaultSortingParameters<TEntity>
    >
    where TEntity : class, IEntity<TId, TIdValue>
    where TId : IEntityId<TId, TIdValue>
    where TIdValue :
    IComparable<TIdValue>,
    IComparable,
    IEquatable<TIdValue>,
    ISpanFormattable,
    ISpanParsable<TIdValue>,
    IUtf8SpanFormattable
    where TFilterParameters : BaseFilterParameters<TEntity>, new()
    where TIncludeOptions : DefaultIncludeOptions<TEntity>, new();

public interface IReadOnlyRepository<TEntity>
    : IReadOnlyRepository<
        TEntity,
        DefaultFilterParameters<TEntity>,
        DefaultIncludeOptions<TEntity>,
        DefaultQueryOptions,
        DefaultPaginationParameters
    >
    where TEntity : class, IEntity;

public interface IReadOnlyRepository<TEntity, in TFilterParameters>
    : IReadOnlyRepository<
        TEntity,
        TFilterParameters,
        DefaultIncludeOptions<TEntity>,
        DefaultQueryOptions,
        DefaultPaginationParameters
    >
    where TFilterParameters : BaseFilterParameters<TEntity>, new()
    where TEntity : class, IEntity;

public interface IReadOnlyRepository<TEntity, in TFilterParameters, in TIncludeOptions>
    : IReadOnlyRepository<
        TEntity,
        TFilterParameters,
        TIncludeOptions,
        DefaultQueryOptions,
        DefaultPaginationParameters
    >
    where TIncludeOptions : BaseIncludeOptions<TEntity>, new()
    where TFilterParameters : BaseFilterParameters<TEntity>, new()
    where TEntity : class, IEntity;

public interface IReadOnlyRepository<
    TEntity,
    in TFilterParameters,
    in TIncludeOptions,
    in TQueryOptions
>
    : IReadOnlyRepository<
        TEntity,
        TFilterParameters,
        TIncludeOptions,
        TQueryOptions,
        DefaultPaginationParameters
    >
    where TIncludeOptions : BaseIncludeOptions<TEntity>, new()
    where TFilterParameters : BaseFilterParameters<TEntity>, new()
    where TQueryOptions : BaseQueryOptions, new()
    where TEntity : class, IEntity;

public interface IReadOnlyRepository<
    TEntity,
    in TFilterParameters,
    in TIncludeOptions,
    in TQueryOptions,
    in TPaginationParameters
>
    : IReadOnlyRepository<
        TEntity,
        TFilterParameters,
        TIncludeOptions,
        TQueryOptions,
        TPaginationParameters,
        DefaultSortingParameters<TEntity>
    >
    where TIncludeOptions : BaseIncludeOptions<TEntity>, new()
    where TFilterParameters : BaseFilterParameters<TEntity>, new()
    where TQueryOptions : BaseQueryOptions, new()
    where TPaginationParameters : BasePaginationParameters, new()
    where TEntity : class, IEntity;

public interface IReadOnlyRepository<
    TEntity,
    in TFilterParameters,
    in TIncludeOptions,
    in TQueryOptions,
    in TPaginationParameters,
    in TSortingParameters
>
    where TIncludeOptions : BaseIncludeOptions<TEntity>, new()
    where TFilterParameters : BaseFilterParameters<TEntity>, new()
    where TQueryOptions : BaseQueryOptions, new()
    where TPaginationParameters : BasePaginationParameters, new()
    where TSortingParameters : BaseSortingParameters<TEntity>, new()
    where TEntity : class, IEntity
{
    Task<TEntity?> FindAsync(
        TFilterParameters filterParameters,
        TIncludeOptions? includeOptions = null,
        TQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default);

    Task<TEntity> GetAsync(
        TFilterParameters filterParameters,
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

public interface IReadOnlyIdRepository<
    TEntity,
    in TId,
    in TIdValue,
    in TFilterParameters,
    in TIncludeOptions,
    in TQueryOptions,
    in TPaginationParameters,
    in TSortingParameters
> : IReadOnlyRepository<TEntity, TFilterParameters, TIncludeOptions, TQueryOptions, TPaginationParameters, TSortingParameters>
    where TIncludeOptions : BaseIncludeOptions<TEntity>, new()
    where TFilterParameters : BaseFilterParameters<TEntity>, new()
    where TQueryOptions : BaseQueryOptions, new()
    where TPaginationParameters : BasePaginationParameters, new()
    where TSortingParameters : BaseSortingParameters<TEntity>, new()
    where TEntity : class, IEntity<TId, TIdValue>
    where TId :
    IEntityId<TId, TIdValue>
    where TIdValue :
    IComparable<TIdValue>,
    IComparable,
    IEquatable<TIdValue>,
    ISpanFormattable,
    ISpanParsable<TIdValue>,
    IUtf8SpanFormattable
{
    Task<TEntity?> FindAsync(
        TId id,
        TFilterParameters? filterParameters = null,
        TIncludeOptions? includeOptions = null,
        TQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default);

    Task<TEntity> GetAsync(
        TId id,
        TFilterParameters? filterParameters = null,
        TIncludeOptions? includeOptions = null,
        TQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default);
}