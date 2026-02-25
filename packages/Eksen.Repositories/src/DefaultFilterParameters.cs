using System.Linq.Expressions;
using Eksen.ValueObjects.Entities;

namespace Eksen.Repositories;

public record DefaultFilterParameters<TEntity> : BaseFilterParameters<TEntity>
    where TEntity : class, IEntity
{
    public Expression<Func<TEntity, bool>>? Predicate { get; set; }
}