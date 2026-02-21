using Eksen.Ulid;

namespace Eksen.Entities.Users;

public sealed record EksenUserId(System.Ulid Value) : UlidEntityId<EksenUserId>(Value);