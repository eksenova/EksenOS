using Eksen.ValueObjects.Entities;

namespace Eksen.Authentication.ApiKeys;

public interface IEksenApiKey<out TId> : IEntity<TId, System.Ulid>
    where TId : IEntityId<TId, System.Ulid>
{
    ApiKeyName Name { get; }
    ApiKeyValue KeyValue { get; }
    DateTime? ExpiresAt { get; }
    DateTime? RevokedAt { get; }
    bool IsRevoked { get; }
    bool IsExpired { get; }
    bool IsActive { get; }
}
