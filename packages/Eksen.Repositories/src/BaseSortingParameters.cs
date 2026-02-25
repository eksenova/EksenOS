using Eksen.ValueObjects.Entities;

namespace Eksen.Repositories;

public record BaseSortingParameters<TEntity>
    where TEntity : class, IEntity;

public record DefaultSortingParameters<TEntity> : BaseSortingParameters<TEntity>
    where TEntity : class, IEntity
{
    public string? Sorting { get; set; }
}