using System.Linq.Expressions;
using Eksen.Repositories;

namespace Eksen.Permissions;

public interface IPermissionDefinitionRepository : IIdRepository<PermissionDefinition, PermissionDefinitionId, System.Ulid>
{
    public Task<long> CountAsync(
        Expression<Func<PermissionDefinition, bool>>? predicate = null,
        string? searchFilter = null,
        bool ignoreQueryFilters = false,
        bool ignoreAutoIncludes = false,
        CancellationToken cancellationToken = default);
     
    Task<ICollection<PermissionDefinition>> GetListAsync(
        Expression<Func<PermissionDefinition, bool>>? predicate = null,
        ICollection<Expression<Func<PermissionDefinition, object>>>? includes = null,
        string? searchFilter = null,
        string? sorting = null,
        int? skipCount = null,
        int? maxResultCount = null,
        bool ignoreQueryFilters = false,
        bool ignoreAutoIncludes = false,
        bool asNoTracking = false,
        CancellationToken cancellationToken = default);
}