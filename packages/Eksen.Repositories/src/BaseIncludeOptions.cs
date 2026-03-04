using System.Linq.Expressions;

namespace Eksen.Repositories;

public record BaseIncludeOptions<TEntity>
{
    public ICollection<Expression<Func<TEntity, object>>>? Includes { get; set; }

    public bool IgnoreAutoIncludes { get; set; }


    public static implicit operator BaseIncludeOptions<TEntity>(List<Expression<Func<TEntity, object>>> includeExpressions)
    {
        return new BaseIncludeOptions<TEntity>
        {
            Includes = includeExpressions
        };
    }

    public static implicit operator BaseIncludeOptions<TEntity>(Expression<Func<TEntity, object>>[] includeExpressions)
    {
        return new BaseIncludeOptions<TEntity>
        {
            Includes = includeExpressions
        };
    }
}

