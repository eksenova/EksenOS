using System.Security.Claims;
using Eksen.Identity.Claims;
using Eksen.Identity.Tenants;
using Eksen.Identity.Users;
using Eksen.ValueObjects.Entities;

namespace Eksen.Authentication.ApiKeys.Identity;

public class DefaultUserApiKeyAuthenticator<TUser, TTenant>(
    IEksenUserApiKeyRepository<TUser, TTenant> apiKeyRepository)
    : IApiKeyAuthenticator<EksenUserApiKey<TUser, TTenant>, EksenUserApiKeyId>
    where TUser : class, IEksenUser<TTenant>
    where TTenant : class, IEksenTenant
{
    public async Task<ApiKeyAuthenticationResult> AuthenticateAsync(
        string apiKeyValue,
        CancellationToken cancellationToken = default)
    {
        if (!ApiKeyValue.TryCreate(apiKeyValue, out var parsedKeyValue))
        {
            return ApiKeyAuthenticationResult.Fail("Invalid API key format.");
        }

        var apiKey = await apiKeyRepository.FindByKeyValueAsync(parsedKeyValue, cancellationToken);

        if (apiKey == null)
        {
            return ApiKeyAuthenticationResult.Fail("API key not found.");
        }

        if (apiKey.IsRevoked)
        {
            return ApiKeyAuthenticationResult.Fail("API key has been revoked.");
        }

        if (apiKey.IsExpired)
        {
            return ApiKeyAuthenticationResult.Fail("API key has expired.");
        }

        if (!apiKey.User.IsActive)
        {
            return ApiKeyAuthenticationResult.Fail("User is not active.");
        }

        var principal = BuildClaimsPrincipal(apiKey);
        return ApiKeyAuthenticationResult.Success(principal);
    }

    protected virtual ClaimsPrincipal BuildClaimsPrincipal(EksenUserApiKey<TUser, TTenant> apiKey)
    {
        var user = apiKey.User;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.Value.ToString())
        };

        if (user.EmailAddress != null)
        {
            claims.Add(new Claim(ClaimTypes.Email, user.EmailAddress.Value));
        }

        if (apiKey.Tenant != null)
        {
            claims.Add(new Claim(EksenClaims.TenantId, apiKey.Tenant.Id.Value.ToString()));
            claims.Add(new Claim(EksenClaims.TenantName, apiKey.Tenant.Name.Value));
        }

        var identity = new ClaimsIdentity(claims, "ApiKey");
        return new ClaimsPrincipal(identity);
    }
}
