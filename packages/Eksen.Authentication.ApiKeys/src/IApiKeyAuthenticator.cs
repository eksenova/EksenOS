using System.Security.Claims;
using Eksen.ValueObjects.Entities;

namespace Eksen.Authentication.ApiKeys;

public interface IApiKeyAuthenticator<TApiKey, TId>
    where TApiKey : class, IEksenApiKey<TId>
    where TId : IEntityId<TId, System.Ulid>
{
    Task<ApiKeyAuthenticationResult> AuthenticateAsync(
        string apiKeyValue,
        CancellationToken cancellationToken = default);
}

public sealed class ApiKeyAuthenticationResult
{
    public bool IsAuthenticated { get; private init; }
    public ClaimsPrincipal? Principal { get; private init; }
    public string? FailureReason { get; private init; }

    public static ApiKeyAuthenticationResult Success(ClaimsPrincipal principal)
    {
        return new ApiKeyAuthenticationResult
        {
            IsAuthenticated = true,
            Principal = principal
        };
    }

    public static ApiKeyAuthenticationResult Fail(string reason)
    {
        return new ApiKeyAuthenticationResult
        {
            IsAuthenticated = false,
            FailureReason = reason
        };
    }
}
