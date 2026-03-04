using Eksen.ValueObjects.Entities;

namespace Eksen.Repositories;

public record BaseSortingParameters<TEntity>
    where TEntity : class, IEntity
{
    public string? Sorting { get; set; }

    public static implicit operator BaseSortingParameters<TEntity>(string sorting)
    {
        return new BaseSortingParameters<TEntity>
        {
            Sorting = sorting
        };
    }
}

public record DefaultSortingParameters<TEntity> : BaseSortingParameters<TEntity>
    where TEntity : class, IEntity;