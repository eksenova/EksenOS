namespace Eksen.Scalar;

/// <summary>
/// How the impersonation plugin acts as the chosen subject.
/// </summary>
public enum ScalarImpersonationMode
{
    /// <summary>
    /// Exchanges the caller's bearer at <see cref="ScalarImpersonationOptions.TokenEndpoint"/> for a token scoped to
    /// the subject and sends that token on API calls.
    /// </summary>
    TokenExchange,

    /// <summary>
    /// Keeps the caller's own token and sends the subject id in <see cref="ScalarImpersonationOptions.HeaderName"/>.
    /// The server decides whether the caller may impersonate.
    /// </summary>
    Header
}

/// <summary>
/// Options for the impersonation plugin, passed to
/// <see cref="EksenScalarExtensions.WithEksenImpersonationPlugin"/>. The control appears only for callers whose
/// token the <see cref="SubjectsEndpoint"/> accepts with 200.
/// </summary>
public sealed class ScalarImpersonationOptions
{
    /// <summary>
    /// How the subject is impersonated. Defaults to <see cref="ScalarImpersonationMode.TokenExchange"/>.
    /// </summary>
    public ScalarImpersonationMode Mode { get; set; } = ScalarImpersonationMode.TokenExchange;

    /// <summary>
    /// The endpoint listing the subjects that can be impersonated. The plugin also uses it to decide whether
    /// the caller may impersonate at all. Defaults to a paged, name-sorted tenants query.
    /// </summary>
    public string SubjectsEndpoint { get; set; } = "/api/tenants?MaxResultCount=1000&Sorting=Name%20ASC";

    /// <summary>
    /// The former name of <see cref="SubjectsEndpoint"/>.
    /// </summary>
    [Obsolete("Use SubjectsEndpoint.")]
    public string TenantsEndpoint
    {
        get
        {
            return SubjectsEndpoint;
        }
        set
        {
            SubjectsEndpoint = value;
        }
    }

    /// <summary>
    /// When set, typing in the search box queries <see cref="SubjectsEndpoint"/> with this query parameter
    /// instead of filtering the first page locally. Use it for large subject sets such as users.
    /// </summary>
    public string? SearchParameter { get; set; }

    /// <summary>
    /// Dotted path of the item array in the subjects response. Ignored when the response is an array.
    /// Defaults to <c>items</c>.
    /// </summary>
    public string ItemsPath { get; set; } = "items";

    /// <summary>
    /// Dotted path of the subject id within an item. Defaults to <c>id</c>.
    /// </summary>
    public string IdField { get; set; } = "id";

    /// <summary>
    /// Dotted path of the subject's display name within an item. Defaults to <c>name</c>.
    /// </summary>
    public string NameField { get; set; } = "name";

    /// <summary>
    /// Optional dotted path of a secondary line, such as an e-mail address.
    /// </summary>
    public string? DetailField { get; set; }

    /// <summary>
    /// The request header that carries the subject id in <see cref="ScalarImpersonationMode.Header"/> mode.
    /// Defaults to <c>X-Impersonate</c>.
    /// </summary>
    public string HeaderName { get; set; } = "X-Impersonate";

    /// <summary>
    /// Same-origin path prefixes whose requests are made as the subject. Defaults to <c>/api/</c>.
    /// </summary>
    public IList<string> ApiPathPrefixes { get; } = ["/api/"];

    /// <summary>
    /// The OAuth2 token endpoint used to mint the impersonation token. Defaults to <c>/connect/token</c>.
    /// </summary>
    public string TokenEndpoint { get; set; } = "/connect/token";

    /// <summary>
    /// The OAuth2 grant type used to request the impersonation token. Defaults to <c>tenant_impersonation</c>.
    /// </summary>
    public string GrantType { get; set; } = "tenant_impersonation";

    /// <summary>
    /// The token request parameter that carries the subject id. Defaults to <c>tenant_id</c>.
    /// </summary>
    public string SubjectParameter { get; set; } = "tenant_id";

    /// <summary>
    /// The fallback OAuth2 client id used if one cannot be derived from the captured token. Defaults to <c>scalar</c>.
    /// </summary>
    public string ClientId { get; set; } = "scalar";

    /// <summary>
    /// Optional JWT claim a token must carry before the plugin offers impersonation. Defaults to <c>is_host</c>;
    /// set it to <see langword="null"/> when the subjects endpoint alone decides.
    /// </summary>
    public string? HostClaim { get; set; } = "is_host";

    /// <summary>
    /// The JWT claim that marks a token as already impersonating. Defaults to <c>is_impersonating</c>.
    /// </summary>
    public string ImpersonatingClaim { get; set; } = "is_impersonating";

    /// <summary>
    /// Visible text, keyed by <c>open</c>, <c>active</c>, <c>title</c>, <c>search</c>, <c>stop</c>,
    /// <c>loading</c>, <c>switching</c>, <c>empty</c> and <c>close</c>. Missing keys keep the English defaults.
    /// </summary>
    public IDictionary<string, string> Text { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
