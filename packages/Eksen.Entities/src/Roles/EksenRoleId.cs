using Eksen.Ulid;

namespace Eksen.Entities.Roles;

public sealed record EksenRoleId(System.Ulid Value) : UlidEntityId<EksenRoleId>(Value);