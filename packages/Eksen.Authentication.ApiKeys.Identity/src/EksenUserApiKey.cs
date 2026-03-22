using Eksen.Entities;
using Eksen.Identity;
using Eksen.Identity.Tenants;
using Eksen.Identity.Users;
using Eksen.ValueObjects.Entities;

namespace Eksen.Authentication.ApiKeys.Identity;

public class EksenUserApiKey<TUser, TTenant> :
    IEksenApiKey<EksenUserApiKeyId>,
    IHasCreationTime,
    IHasModificationTime,
    IMayHaveTenant<TTenant>
    where TUser : class, IEksenUser<TTenant>
    where TTenant : class, IEksenTenant
{
    public EksenUserApiKeyId Id { get; private set; }
    public ApiKeyName Name { get; private set; }
    public ApiKeyValue KeyValue { get; private set; }
    public TUser User { get; private set; }
    public TTenant? Tenant { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public DateTime CreationTime { get; private set; }
    public DateTime? LastModificationTime { get; private set; }

    public bool IsRevoked => RevokedAt != null;
    public bool IsExpired => ExpiresAt != null && ExpiresAt <= DateTime.UtcNow;
    public bool IsActive => !IsRevoked && !IsExpired;

    private EksenUserApiKey()
    {
        Id = EksenUserApiKeyId.Empty;
        Name = null!;
        KeyValue = null!;
        User = null!;
    }

    public EksenUserApiKey(
        ApiKeyName name,
        ApiKeyValue keyValue,
        TUser user,
        TTenant? tenant,
        DateTime? expiresAt) : this()
    {
        Id = EksenUserApiKeyId.NewId();
        Name = name;
        KeyValue = keyValue;
        User = user;
        Tenant = tenant;
        ExpiresAt = expiresAt;
    }

    public void Revoke()
    {
        if (IsRevoked)
        {
            throw ApiKeyErrors.ApiKeyAlreadyRevoked.Raise();
        }

        RevokedAt = DateTime.UtcNow;
    }

    public void Regenerate(ApiKeyValue newKeyValue)
    {
        if (IsRevoked)
        {
            throw ApiKeyErrors.ApiKeyRevoked.Raise();
        }

        KeyValue = newKeyValue;
    }

    public void SetName(ApiKeyName name)
    {
        Name = name;
    }

    public void SetExpiresAt(DateTime? expiresAt)
    {
        ExpiresAt = expiresAt;
    }
}
