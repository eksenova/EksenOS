using Eksen.ValueObjects.Entities;

namespace Eksen.Repositories;

public interface IIdRepository<TEntity, in TId, in TIdValue>
    : IIdRepository<
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
    where TId : class, IEntityId<TId, TIdValue>
    where TIdValue :
    IComparable<TIdValue>,
    IComparable,
    IEquatable<TIdValue>,
    ISpanFormattable,
    ISpanParsable<TIdValue>,
    IUtf8SpanFormattable;

public interface IIdRepository<TEntity, in TId, in TIdValue, in TFilterParameters>
    : IIdRepository<
        TEntity,
        TId,
        TIdValue,
        TFilterParameters,
        DefaultIncludeOptions<TEntity>,
        DefaultQueryOptions,
        DefaultPaginationParameters,
        DefaultSortingParameters<TEntity>
    >
    where TFilterParameters : BaseFilterParameters<TEntity>, new()
    where TEntity : class, IEntity<TId, TIdValue>
    where TId : class, IEntityId<TId, TIdValue>
    where TIdValue :
    IComparable<TIdValue>,
    IComparable,
    IEquatable<TIdValue>,
    ISpanFormattable,
    ISpanParsable<TIdValue>,
    IUtf8SpanFormattable;

public interface IIdRepository<
    TEntity,
    in TId,
    in TIdValue,
    in TFilterParameters,
    in TIncludeOptions
>
    : IIdRepository<
        TEntity,
        TId,
        TIdValue,
        TFilterParameters,
        TIncludeOptions,
        DefaultQueryOptions,
        DefaultPaginationParameters,
        DefaultSortingParameters<TEntity>
    >
    where TIncludeOptions : BaseIncludeOptions<TEntity>, new()
    where TFilterParameters : BaseFilterParameters<TEntity>, new()
    where TEntity : class, IEntity<TId, TIdValue>
    where TId : class, IEntityId<TId, TIdValue>
    where TIdValue :
    IComparable<TIdValue>,
    IComparable,
    IEquatable<TIdValue>,
    ISpanFormattable,
    ISpanParsable<TIdValue>,
    IUtf8SpanFormattable;

public interface IIdRepository<
    TEntity,
    in TId,
    in TIdValue,
    in TFilterParameters,
    in TIncludeOptions,
    in TQueryOptions,
    in TPaginationParameters, in TSortingParameters>
    : IReadOnlyIdRepository<
        TEntity,
        TId,
        TIdValue,
        TFilterParameters,
        TIncludeOptions,
        TQueryOptions,
        TPaginationParameters,
        TSortingParameters
    >, IRepository<
        TEntity,
        TFilterParameters,
        TIncludeOptions,
        TQueryOptions,
        TPaginationParameters,
        TSortingParameters
    >
    where TIncludeOptions : BaseIncludeOptions<TEntity>, new()
    where TFilterParameters : BaseFilterParameters<TEntity>, new()
    where TQueryOptions : BaseQueryOptions, new()
    where TPaginationParameters : BasePaginationParameters, new()
    where TSortingParameters : BaseSortingParameters<TEntity>, new()
    where TEntity : class, IEntity<TId, TIdValue>
    where TId : class, IEntityId<TId, TIdValue>
    where TIdValue :
    IComparable<TIdValue>,
    IComparable,
    IEquatable<TIdValue>,
    ISpanFormattable,
    ISpanParsable<TIdValue>,
    IUtf8SpanFormattable;

public interface IRepository<TEntity>
    : IRepository<
        TEntity,
        DefaultFilterParameters<TEntity>,
        DefaultIncludeOptions<TEntity>,
        DefaultQueryOptions,
        DefaultPaginationParameters,
        DefaultSortingParameters<TEntity>
    >
    where TEntity : class, IEntity;

public interface IRepository<TEntity, in TFilterParameters, in TIncludeOptions, in TQueryOptions>
    : IRepository<
        TEntity,
        TFilterParameters,
        TIncludeOptions,
        TQueryOptions,
        DefaultPaginationParameters,
        DefaultSortingParameters<TEntity>
    >
    where TEntity : class, IEntity
    where TIncludeOptions : BaseIncludeOptions<TEntity>, new()
    where TFilterParameters : BaseFilterParameters<TEntity>, new()
    where TQueryOptions : BaseQueryOptions, new();

public interface IRepository<
    TEntity,
    in TFilterParameters,
    in TIncludeOptions,
    in TQueryOptions,
    in TPaginationParameters
>
    : IRepository<
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

public interface IRepository<TEntity, in TFilterParameters, in TIncludeOptions, in TQueryOptions,
    in TPaginationParameters, in TSortingParameters>
    : IReadOnlyRepository<TEntity, TFilterParameters, TIncludeOptions, TQueryOptions, TPaginationParameters,
        TSortingParameters>
    where TIncludeOptions : BaseIncludeOptions<TEntity>, new()
    where TFilterParameters : BaseFilterParameters<TEntity>, new()
    where TQueryOptions : BaseQueryOptions, new()
    where TPaginationParameters : BasePaginationParameters, new()
    where TSortingParameters : BaseSortingParameters<TEntity>, new()
    where TEntity : class, IEntity
{
    Task InsertAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default);

    Task InsertManyAsync(IEnumerable<TEntity> entities, bool autoSave = false, CancellationToken cancellationToken = default);

    Task RemoveAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default);

    Task RemoveAsync(TFilterParameters predicate, bool autoSave = false, CancellationToken cancellationToken = default);

    Task RemoveManyAsync(IEnumerable<TEntity> entities, bool autoSave = false, CancellationToken cancellationToken = default);

    Task UpdateAsync(TEntity entity, bool autoSave = false, CancellationToken cancellationToken = default);

    Task UpdateManyAsync(IEnumerable<TEntity> entities, bool autoSave = false, CancellationToken cancellationToken = default);
}