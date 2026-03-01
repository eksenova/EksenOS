using Eksen.ValueObjects.Entities;

namespace Eksen.Repositories;

public record DefaultFilterParameters<TEntity> : BaseFilterParameters<TEntity>
    where TEntity : class, IEntity;