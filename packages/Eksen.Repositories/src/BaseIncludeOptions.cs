using System.Linq.Expressions;

namespace Eksen.Repositories;

public record BaseIncludeOptions<TEntity>
{
    public ICollection<Expression<Func<TEntity, object>>>? Includes { get; set; }

    public bool IgnoreAutoIncludes { get; set; }
}