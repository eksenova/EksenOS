namespace Eksen.Scalar;

/// <summary>
/// Options for the tenant impersonation plugin, passed to
/// <see cref="EksenScalarExtensions.WithEksenImpersonationPlugin"/>. Host users can exchange their
/// bearer token for a tenant-scoped token and have it injected into subsequent API calls.
/// </summary>
public sealed class ScalarImpersonationOptions
{
    /// <summary>
    /// The OAuth2 token endpoint used to mint the impersonation token. Defaults to <c>/connect/token</c>.
    /// </summary>
    public string TokenEndpoint { get; set; } = "/connect/token";

    /// <summary>
    /// The endpoint returning the list of impersonatable tenants.
    /// Defaults to a paged, name-sorted tenants query.
    /// </summary>
    public string TenantsEndpoint { get; set; } = "/api/tenants?MaxResultCount=1000&Sorting=Name%20ASC";

    /// <summary>
    /// The OAuth2 grant type used to request the impersonation token. Defaults to <c>tenant_impersonation</c>.
    /// </summary>
    public string GrantType { get; set; } = "tenant_impersonation";

    /// <summary>
    /// The fallback OAuth2 client id used if one cannot be derived from the captured token. Defaults to <c>scalar</c>.
    /// </summary>
    public string ClientId { get; set; } = "scalar";

    /// <summary>
    /// The JWT claim that marks a token as belonging to a host (non-tenant) user. Defaults to <c>is_host</c>.
    /// </summary>
    public string HostClaim { get; set; } = "is_host";

    /// <summary>
    /// The JWT claim that marks a token as already impersonating. Defaults to <c>is_impersonating</c>.
    /// </summary>
    public string ImpersonatingClaim { get; set; } = "is_impersonating";
}
