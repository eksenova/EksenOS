using System.Text.Encodings.Web;
using Eksen.Authentication.ApiKeys;
using Eksen.ValueObjects.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eksen.Authentication.ApiKeys.AspNetCore;

public class ApiKeyAuthenticationHandler<TApiKey, TId>(
    IOptionsMonitor<ApiKeyAuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IApiKeyAuthenticator<TApiKey, TId> authenticator)
    : AuthenticationHandler<ApiKeyAuthenticationSchemeOptions>(options, logger, encoder)
    where TApiKey : class, IEksenApiKey<TId>
    where TId : IEntityId<TId, System.Ulid>
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authenticationMethod = Options.AuthenticationMethod;
        string? apiKeyValue = null;

        switch (authenticationMethod)
        {
            case CustomHeaderAuthenticationMethod customHeader:
            {
                if (Request.Headers.TryGetValue(customHeader.HeaderName, out var headerValues))
                {
                    apiKeyValue = headerValues.FirstOrDefault();
                }

                break;
            }
            case AuthorizationHeaderAuthenticationMethod authorizationHeader:
            {
                var authHeader = Request.Headers.Authorization.FirstOrDefault();
                if (authHeader != null)
                {
                    var prefix = authorizationHeader.Scheme + " ";
                    if (authHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        apiKeyValue = authHeader[prefix.Length..].Trim();
                    }
                }

                break;
            }
        }

        if (string.IsNullOrWhiteSpace(apiKeyValue))
        {
            return AuthenticateResult.NoResult();
        }

        var result = await authenticator.AuthenticateAsync(apiKeyValue, Context.RequestAborted);

        if (!result.IsAuthenticated)
        {
            return AuthenticateResult.Fail(result.FailureReason ?? "API key authentication failed.");
        }

        var ticket = new AuthenticationTicket(result.Principal!, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }
}
