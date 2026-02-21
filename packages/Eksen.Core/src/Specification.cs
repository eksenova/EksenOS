using System.Linq.Expressions;

namespace Eksen.Core;

public interface ISpecification<TObj>
{
    bool IsSatisfiedBy(TObj obj);
    
    Expression<Func<TObj, bool>> ToExpression();
}

public abstract class Specification<TObj> : ISpecification<TObj>
{
    public abstract Expression<Func<TObj, bool>> ToExpression();

    public bool IsSatisfiedBy(TObj obj)
    {
        var predicate = ToExpression();
        return predicate.Compile()(obj);
    }

    public static implicit operator Expression<Func<TObj, bool>>(Specification<TObj> specification)
    {
        return specification.ToExpression();
    }
}