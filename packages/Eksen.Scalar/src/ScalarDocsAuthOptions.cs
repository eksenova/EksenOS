namespace Eksen.Scalar;

/// <summary>
/// Options for the docs sign-in wall, passed to <see cref="EksenScalarExtensions.WithEksenDocsAuthPlugin"/>.
/// The wall signs the reader in with the OpenID Connect authorization-code flow and PKCE in a popup, sends the
/// access token with the document request and same-origin API calls, refreshes it, and returns to the wall when
/// the server answers the document request with 401 or 403. The server stays the authority on who may read.
/// Map the popup's landing page with <see cref="EksenScalarExtensions.MapEksenScalarOAuthCallback"/>.
/// </summary>
public sealed class ScalarDocsAuthOptions
{
    /// <summary>
    /// The provider's authorization endpoint, for example
    /// <c>https://id.example.com/realms/main/protocol/openid-connect/auth</c>.
    /// </summary>
    public required string AuthorizeEndpoint { get; set; }

    /// <summary>
    /// The provider's token endpoint. The provider must allow cross-origin calls from the docs origin.
    /// </summary>
    public required string TokenEndpoint { get; set; }

    /// <summary>
    /// Optional end-session endpoint. When set, signing out also ends the provider session.
    /// </summary>
    public string? EndSessionEndpoint { get; set; }

    /// <summary>
    /// The public OAuth2 client registered for the docs, with the callback URL as a valid redirect URI.
    /// </summary>
    public required string ClientId { get; set; }

    /// <summary>
    /// Requested scopes. Defaults to <c>openid</c>.
    /// </summary>
    public string Scope { get; set; } = "openid";

    /// <summary>
    /// Path of the popup landing page mapped with <see cref="EksenScalarExtensions.MapEksenScalarOAuthCallback"/>.
    /// Defaults to <see cref="EksenScalarExtensions.DefaultOAuthCallbackPath"/>.
    /// </summary>
    public string CallbackPath { get; set; } = EksenScalarExtensions.DefaultOAuthCallbackPath;

    /// <summary>
    /// The path prefix of the gated OpenAPI document. Defaults to <c>/openapi/internal</c>.
    /// </summary>
    public string DocumentPathPrefix { get; set; } = "/openapi/internal";

    /// <summary>
    /// Same-origin path prefixes that receive the reader's token when a request has no Authorization header of
    /// its own. Defaults to <c>/api/</c>.
    /// </summary>
    public IList<string> ApiPathPrefixes { get; } = ["/api/"];

    /// <summary>
    /// Session storage key for the tokens. Use a distinct key per gated reference.
    /// </summary>
    public string StorageKey { get; set; } = "eksen.scalar.docs-auth";

    /// <summary>
    /// Optional client-side precheck before the document is requested: a dotted claim path (for example
    /// <c>realm_access.roles</c>) and accepted values. The server decides regardless.
    /// </summary>
    public ScalarRequiredClaim? RequiredClaim { get; set; }

    /// <summary>
    /// Heading of the sign-in card.
    /// </summary>
    public string Title { get; set; } = "Internal API";

    /// <summary>
    /// One-line explanation under the heading.
    /// </summary>
    public string Subtitle { get; set; } = "Sign in with an account that has access to this document.";

    /// <summary>
    /// Optional short line above the heading.
    /// </summary>
    public string? Eyebrow { get; set; }

    /// <summary>
    /// Optional logo shown in the card's brand band.
    /// </summary>
    public string? LogoUrl { get; set; }

    /// <summary>
    /// Accessible name of <see cref="LogoUrl"/>.
    /// </summary>
    public string LogoAltText { get; set; } = string.Empty;

    /// <summary>
    /// Visible text, keyed by <c>signIn</c>, <c>waiting</c>, <c>hint</c>, <c>signedInAs</c>, <c>signOut</c>,
    /// <c>expired</c>, <c>forbidden</c>, <c>popupBlocked</c>, <c>failed</c> and <c>missingClaim</c>. Missing keys
    /// keep the English defaults.
    /// </summary>
    public IDictionary<string, string> Text { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>
/// A claim the docs sign-in wall checks before requesting the document.
/// </summary>
/// <param name="Name">Dotted claim path, for example <c>realm_access.roles</c>.</param>
/// <param name="Values">Accepted values; any match passes. Empty means the claim only has to be present.</param>
public sealed record ScalarRequiredClaim(string Name, IReadOnlyList<string> Values);
