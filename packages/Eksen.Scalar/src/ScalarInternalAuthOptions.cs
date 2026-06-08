namespace Eksen.Scalar;

/// <summary>
/// Options for the internal-auth wall plugin, passed to
/// <see cref="EksenScalarExtensions.WithEksenInternalAuthPlugin"/>. The plugin gates the page behind
/// an in-page sign-in card that only host users (per <see cref="HostClaim"/>) can pass; mapping the
/// internal Scalar reference itself (route, title, documents) is the consumer's responsibility.
/// </summary>
public sealed class ScalarInternalAuthOptions
{
    /// <summary>
    /// The OpenAPI path prefix of the internal document that the auth wall must gate. Defaults to <c>/openapi/internal</c>.
    /// </summary>
    public string InternalDocumentPath { get; set; } = "/openapi/internal";

    /// <summary>
    /// The token endpoint used by the auth wall to sign in. Defaults to <c>/connect/token</c>.
    /// </summary>
    public string TokenEndpoint { get; set; } = "/connect/token";

    /// <summary>
    /// The OAuth2 client id used by the auth-wall sign-in form.
    /// </summary>
    public string ClientId { get; set; } = "scalar";

    /// <summary>
    /// The OAuth2 client secret used by the auth-wall sign-in form. Usually empty for public clients.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// The default username pre-filled on the auth-wall sign-in form.
    /// </summary>
    public string? DefaultUsername { get; set; }

    /// <summary>
    /// The JWT claim that marks a token as belonging to a host user. Only host tokens pass the auth wall.
    /// Defaults to <c>is_host</c>.
    /// </summary>
    public string HostClaim { get; set; } = "is_host";

    /// <summary>
    /// The brand / heading text shown on the auth-wall card.
    /// </summary>
    public string BrandText { get; set; } = "Internal API";

    /// <summary>
    /// The subtitle text shown on the auth-wall card explaining the host-only restriction.
    /// </summary>
    public string SubtitleText { get; set; } = "This document is available to host users only.";
}
