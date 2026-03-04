using System.Linq.Expressions;

namespace Eksen.Repositories;

public record BaseFilterParameters<TEntity>
{
    public Expression<Func<TEntity, bool>>? Predicate { get; set; }

    public static implicit operator BaseFilterParameters<TEntity>(Expression<Func<TEntity, bool>> expression)
    {
        return new BaseFilterParameters<TEntity>
        {
            Predicate = expression
        };
    }

    public virtual Expression<Func<TEntity, bool>>? ToFilterExpression()
    {
        return Predicate;
    }
}