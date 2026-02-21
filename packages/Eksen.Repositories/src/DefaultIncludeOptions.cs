using System.Linq.Expressions;

namespace Eksen.Repositories;

public record DefaultIncludeOptions<TEntity> : BaseIncludeOptions<TEntity>
{
    public ICollection<Expression<Func<TEntity, object>>>? Includes { get; set; }
}