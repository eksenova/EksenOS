using System.Text.Json.Serialization;
using Eksen.Entities;
using Eksen.Ulid;
using Eksen.ValueObjects.Entities;
using JetBrains.Annotations;

namespace Eksen.Permissions;

public sealed record PermissionDefinitionId(System.Ulid Value) : UlidEntityId<PermissionDefinitionId>(Value);

public sealed class PermissionDefinition : IEntity<PermissionDefinitionId, System.Ulid>, ISoftDelete
{
    [JsonInclude]
    public PermissionDefinitionId Id { get; [UsedImplicitly] private set; }
 
    [JsonInclude]
    public bool IsDeleted { get; [UsedImplicitly] private set; }

    [JsonInclude]
    public bool IsDisabled { get; [UsedImplicitly] private set; }

    [JsonInclude]
    public PermissionName Name { get; [UsedImplicitly] private set; }

    [JsonConstructor]
    private PermissionDefinition()
    {
        Id = PermissionDefinitionId.Empty;
        Name = null!;
    }

    public PermissionDefinition(PermissionName name) : this()
    {
        Id = PermissionDefinitionId.NewId();
        SetName(name);
    }

    public void SetIsEnabled(bool isEnabled)
    {
        IsDisabled = !isEnabled;
    }

    public void SetName(PermissionName permissionName)
    {
        Name = permissionName;
    }
}