using Eksen.ValueObjects.Entities;

namespace Eksen.Repositories;

public interface IRepository<TEntity, in TFindParameters>
    : IRepository<TEntity,
        DefaultFilterParameters<TEntity>,
        TFindParameters,
        DefaultIncludeOptions<TEntity>,
        DefaultQueryOptions,
        DefaultPaginationParameters,
        DefaultSortingParameters
    >
    where TFindParameters : DefaultFilterParameters<TEntity>
    where TEntity : class, IEntity;

public interface IIdRepository<TEntity, TId, TIdValue>
    : IRepository<TEntity,
        DefaultFilterParameters<TEntity>,
        DefaultIdFindParameters<TId, TIdValue>,
        DefaultIncludeOptions<TEntity>,
        DefaultQueryOptions,
        DefaultPaginationParameters,
        DefaultSortingParameters
    >
    where TEntity : class, IEntity<TId, TIdValue>
    where TId :
    IEntityId<TId, TIdValue>
    where TIdValue :
    IComparable<TIdValue>,
    IComparable,
    IEquatable<TIdValue>,
    ISpanFormattable,
    ISpanParsable<TIdValue>,
    IUtf8SpanFormattable;

public interface IIdRepository<TEntity, TId, TIdValue, in TIncludeOptions>
    : IRepository<TEntity,
        DefaultFilterParameters<TEntity>,
        DefaultIdFindParameters<TId, TIdValue>,
        TIncludeOptions,
        DefaultQueryOptions,
        DefaultPaginationParameters,
        DefaultSortingParameters
    >
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
    where TIncludeOptions : BaseIncludeOptions<TEntity>, new();

public interface IIdRepository<TEntity, TId, TIdValue, in TIncludeOptions, in TFilterParameters>
    : IRepository<
        TEntity,
        TFilterParameters,
        DefaultIdFindParameters<TId, TIdValue>,
        TIncludeOptions,
        DefaultQueryOptions,
        DefaultPaginationParameters,
        DefaultSortingParameters
    >
    where TEntity : class, IEntity<TId, TIdValue>
    where TFilterParameters : DefaultFilterParameters<TEntity>, new()
    where TId :
    IEntityId<TId, TIdValue>
    where TIdValue :
    IComparable<TIdValue>,
    IComparable,
    IEquatable<TIdValue>,
    ISpanFormattable,
    ISpanParsable<TIdValue>,
    IUtf8SpanFormattable
    where TIncludeOptions : BaseIncludeOptions<TEntity>, new();

public interface IRepository<TEntity, in TFilterParameters, in TFindParameters>
    : IRepository<TEntity, TFilterParameters, TFindParameters, DefaultIncludeOptions<TEntity>, DefaultQueryOptions,
        DefaultPaginationParameters,
        DefaultSortingParameters>
    where TFilterParameters : BaseFilterParameters<TEntity>, new()
    where TFindParameters : class
    where TEntity : class, IEntity;

public interface IRepository<TEntity, in TFilterParameters, in TFindParameters, in TIncludeOptions>
    : IRepository<TEntity, TFilterParameters, TFindParameters, TIncludeOptions, DefaultQueryOptions, DefaultPaginationParameters,
        DefaultSortingParameters>
    where TIncludeOptions : BaseIncludeOptions<TEntity>, new()
    where TFilterParameters : BaseFilterParameters<TEntity>, new()
    where TFindParameters : TFilterParameters
    where TEntity : class, IEntity;

public interface IRepository<TEntity, in TFilterParameters, in TFindParameters, in TIncludeOptions, in TQueryOptions>
    : IRepository<TEntity, TFilterParameters, TFindParameters, TIncludeOptions, TQueryOptions, DefaultPaginationParameters,
        DefaultSortingParameters>
    where TIncludeOptions : BaseIncludeOptions<TEntity>, new()
    where TFilterParameters : BaseFilterParameters<TEntity>, new()
    where TFindParameters : class
    where TQueryOptions : BaseQueryOptions, new()
    where TEntity : class, IEntity;

public interface IRepository<TEntity, in TFilterParameters, in TFindParameters, in TIncludeOptions, in TQueryOptions,
    in TPaginationParameters>
    : IRepository<TEntity, TFilterParameters, TFindParameters, TIncludeOptions, TQueryOptions, TPaginationParameters,
        DefaultSortingParameters>
    where TIncludeOptions : BaseIncludeOptions<TEntity>, new()
    where TFilterParameters : BaseFilterParameters<TEntity>, new()
    where TFindParameters : TFilterParameters
    where TQueryOptions : BaseQueryOptions, new()
    where TPaginationParameters : BasePaginationParameters, new()
    where TEntity : class, IEntity;

public interface IRepository<TEntity, in TFilterParameters, in TFindParameters, in TIncludeOptions, in TQueryOptions,
    in TPaginationParameters, in TSortingParameters>
    : IReadOnlyRepository<TEntity, TFilterParameters, TFindParameters, TIncludeOptions, TQueryOptions, TPaginationParameters,
        TSortingParameters>
    where TIncludeOptions : BaseIncludeOptions<TEntity>, new()
    where TFilterParameters : BaseFilterParameters<TEntity>, new()
    where TFindParameters : class
    where TQueryOptions : BaseQueryOptions, new()
    where TPaginationParameters : BasePaginationParameters, new()
    where TSortingParameters : BaseSortingParameters, new()
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