using Eksen.Ulid;

namespace Eksen.Identity.Users;

public sealed record EksenUserId(System.Ulid Value) : UlidEntityId<EksenUserId>(Value);