using Eksen.Repositories;

namespace Eksen.Permissions;

public interface IEksenPermissionDefinitionRepository
    : IIdRepository<PermissionDefinition, PermissionDefinitionId, System.Ulid, PermissionFilterParameters>;

public record PermissionFilterParameters : BaseFilterParameters<PermissionDefinition>
{
    public bool? IsDisabled { get; set; }
}