using System.Linq.Expressions;

namespace Eksen.Repositories;

public record BaseFilterParameters<TEntity>
{
    public Expression<Func<TEntity, bool>>? Predicate { get; set; }
}