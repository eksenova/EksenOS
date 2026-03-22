using Eksen.Authentication.ApiKeys;
using Eksen.ValueObjects.Entities;

namespace Eksen.Authentication.ApiKeys.AspNetCore;

public class EksenApiKeyAspNetCoreOptions<TApiKey, TId>
    where TApiKey : class, IEksenApiKey<TId>
    where TId : IEntityId<TId, System.Ulid>
{
    public required string Scheme { get; init; }
    public required EksenApiKeyAuthenticationMethod AuthenticationMethod { get; init; }
}
