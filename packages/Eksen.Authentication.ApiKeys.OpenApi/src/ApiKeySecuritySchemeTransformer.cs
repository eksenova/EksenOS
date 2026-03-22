using Eksen.Authentication.ApiKeys;
using Eksen.Authentication.ApiKeys.AspNetCore;
using Eksen.ValueObjects.Entities;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Eksen.Authentication.ApiKeys.OpenApi;

public class ApiKeySecuritySchemeTransformer<TApiKey, TId>(
    EksenApiKeyAspNetCoreOptions<TApiKey, TId> options) : IOpenApiDocumentTransformer
    where TApiKey : class, IEksenApiKey<TId>
    where TId : IEntityId<TId, System.Ulid>
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        var schemeName = options.Scheme;
        var authenticationMethod = options.AuthenticationMethod;

        var securityScheme = authenticationMethod switch
        {
            CustomHeaderAuthenticationMethod customHeader => new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                Name = customHeader.HeaderName,
                In = ParameterLocation.Header,
                Description = $"API key authentication via custom header '{customHeader.HeaderName}'."
            },
            AuthorizationHeaderAuthenticationMethod authorizationHeader => new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = authorizationHeader.Scheme,
                BearerFormat = "API Key",
                Description =
                    $"API key authentication via Authorization header with scheme '{authorizationHeader.Scheme}'."
            },
            _ => throw new NotSupportedException(
                $"Authentication method type '{authenticationMethod.GetType().Name}' is not supported for OpenAPI security scheme generation.")
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[schemeName] = securityScheme;

        var schemeReference = new OpenApiSecuritySchemeReference(schemeName, document);

        var securityRequirement = new OpenApiSecurityRequirement
        {
            { schemeReference, [] }
        };

        foreach (var (_, pathItem) in document.Paths)
        {
            if (pathItem.Operations is null)
            {
                continue;
            }

            foreach (var (_, operation) in pathItem.Operations)
            {
                operation.Security ??= [];
                operation.Security.Add(securityRequirement);
            }
        }

        return Task.CompletedTask;
    }
}
