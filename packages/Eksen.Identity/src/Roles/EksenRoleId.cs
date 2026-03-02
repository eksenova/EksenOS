using Eksen.Ulid;

namespace Eksen.Identity.Roles;

public sealed record EksenRoleId(System.Ulid Value) : UlidEntityId<EksenRoleId>(Value);