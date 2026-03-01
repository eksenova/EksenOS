using System.Linq.Expressions;
using Eksen.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace Eksen.EntityFrameworkCore;

public static class ModelBuilderExtensions
{
    public static void ApplyQueryFilter<TBaseEntity>(
        this ModelBuilder builder,
        Expression<Func<TBaseEntity, bool>> filter)
    {
        var acceptableItems = builder.Model.GetEntityTypes()
            .Where(et => typeof(TBaseEntity).IsAssignableFrom(et.ClrType))
            .ToList();

        foreach (var entityType in acceptableItems)
        {
            var entityParam = Expression.Parameter(entityType.ClrType, name: "e");

            var filterBody = ReplacingExpressionVisitor.Replace(filter.Parameters[index: 0], entityParam, filter.Body);
            var filterLambda = entityType.GetDeclaredQueryFilters().FirstOrDefault(f => f.IsAnonymous)?.Expression;
         
            if (filterLambda != null)
            {
                filterBody = ReplacingExpressionVisitor.Replace(entityParam, filterLambda.Parameters[index: 0], filterBody);
                filterBody = Expression.AndAlso(filterLambda.Body, filterBody);
                filterLambda = Expression.Lambda(filterBody, filterLambda.Parameters);
            }
            else
            {
                filterLambda = Expression.Lambda(filterBody, entityParam);
            }

            entityType.SetQueryFilter(filterLambda);
        }
    }

    public static void ApplyEksenQueryFilters(
        this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplySoftDeleteQueryFilter();
    }

    public static void ApplySoftDeleteQueryFilter(
        this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyQueryFilter<ISoftDelete>(e => !e.IsDeleted);
    }
}