using Eksen.Ulid;

namespace Eksen.Authentication.ApiKeys.Identity;

public sealed record EksenUserApiKeyId(System.Ulid Value) : UlidEntityId<EksenUserApiKeyId>(Value);
