using System.Linq.Expressions;

namespace Eksen.Repositories;

public record DefaultFilterParameters<TEntity> : BaseFilterParameters<TEntity>
{
    public Expression<Func<TEntity, bool>>? Predicate { get; set; }
}