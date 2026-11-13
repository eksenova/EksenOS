using Eksen.Scalar.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Scalar.AspNetCore;

namespace Eksen.Scalar;

/// <summary>
/// Composable <see cref="ScalarOptions"/> extensions that add Eksen's optional client-side plugins to
/// a Scalar API reference. Each plugin is injected as an inline ES-module script on the document head,
/// so they are wired up directly inside the <c>MapScalarApiReference</c> configuration callback and
/// chain freely, for example:
/// <code>
/// app.MapScalarApiReference("/scalar", scalar => scalar
///     .WithEksenLogoPlugin(logo => logo.LogoUrl = "https://cdn.example.com/logo.png")
///     .WithEksenImpersonationPlugin());
/// </code>
/// </summary>
public static class EksenScalarExtensions
{
    /// <summary>
    /// Adds the branding plugin that injects a sidebar logo and footer copyright badge, and can hide
    /// MCP controls and collapse sidebar categories.
    /// </summary>
    public static ScalarOptions WithEksenLogoPlugin(
        this ScalarOptions scalar,
        Action<ScalarLogoOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(scalar);

        var options = new ScalarLogoOptions();
        configure?.Invoke(options);

        EksenScalarScriptBuilder.Inject(
            scalar,
            resourceFileName: "logo.js",
            configGlobalName: "__eksenScalarLogoConfig",
            config: new
            {
                logoUrl = options.LogoUrl,
                logoAltText = options.LogoAltText,
                footerText = options.FooterText,
                footerTitle = options.FooterTitle,
                hideMcpControls = options.HideMcpControls,
                collapseSidebarCategories = options.CollapseSidebarCategories
            });

        return scalar;
    }

    /// <summary>
    /// Adds the auto-fill normalization plugin so password managers behave predictably and
    /// client-credential fields are not auto-filled.
    /// </summary>
    public static ScalarOptions WithEksenAutofillPlugin(this ScalarOptions scalar)
    {
        ArgumentNullException.ThrowIfNull(scalar);

        EksenScalarScriptBuilder.Inject(scalar, resourceFileName: "autofill.js");

        return scalar;
    }

    /// <summary>
    /// Adds the token-body plugin that rewrites token requests so Basic client credentials are moved
    /// from the <c>Authorization</c> header into the form body.
    /// </summary>
    public static ScalarOptions WithEksenTokenBodyPlugin(
        this ScalarOptions scalar,
        Action<ScalarTokenBodyOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(scalar);

        var options = new ScalarTokenBodyOptions();
        configure?.Invoke(options);

        EksenScalarScriptBuilder.Inject(
            scalar,
            resourceFileName: "token-body.js",
            configGlobalName: "__eksenScalarTokenBodyConfig",
            config: new
            {
                tokenEndpoint = options.TokenEndpoint
            });

        return scalar;
    }

    /// <summary>
    /// Adds the impersonation plugin. It appears only for callers the subjects endpoint accepts, and either
    /// exchanges the caller's token for a subject-scoped one or sends the subject id in a header on API calls
    /// (see <see cref="ScalarImpersonationOptions.Mode"/>).
    /// </summary>
    public static ScalarOptions WithEksenImpersonationPlugin(
        this ScalarOptions scalar,
        Action<ScalarImpersonationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(scalar);

        var options = new ScalarImpersonationOptions();
        configure?.Invoke(options);

        InjectDock(scalar);
        EksenScalarScriptBuilder.Inject(
            scalar,
            resourceFileName: "impersonation.js",
            configGlobalName: "__eksenScalarImpersonationConfig",
            config: new
            {
                mode = options.Mode == ScalarImpersonationMode.Header ? "header" : "token",
                subjectsEndpoint = options.SubjectsEndpoint,
                searchParameter = options.SearchParameter,
                itemsPath = options.ItemsPath,
                idField = options.IdField,
                nameField = options.NameField,
                detailField = options.DetailField,
                headerName = options.HeaderName,
                apiPathPrefixes = options.ApiPathPrefixes,
                tokenEndpoint = options.TokenEndpoint,
                grantType = options.GrantType,
                subjectParameter = options.SubjectParameter,
                clientId = options.ClientId,
                hostClaim = options.HostClaim,
                impersonatingClaim = options.ImpersonatingClaim,
                text = options.Text
            });

        return scalar;
    }

    /// <summary>
    /// Adds the docs sign-in wall: the reader signs in with OpenID Connect (authorization code with PKCE) before
    /// the gated document is requested, and the token is sent with the document request and same-origin API
    /// calls. Map the popup landing page with <see cref="MapEksenScalarOAuthCallback"/> and enforce access on the
    /// document endpoint itself; the wall only obtains and presents the token.
    /// </summary>
    public static ScalarOptions WithEksenDocsAuthPlugin(
        this ScalarOptions scalar,
        Action<ScalarDocsAuthOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(scalar);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new ScalarDocsAuthOptions
        {
            AuthorizeEndpoint = string.Empty,
            TokenEndpoint = string.Empty,
            ClientId = string.Empty
        };
        configure(options);

        ArgumentException.ThrowIfNullOrWhiteSpace(options.AuthorizeEndpoint, nameof(options.AuthorizeEndpoint));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.TokenEndpoint, nameof(options.TokenEndpoint));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ClientId, nameof(options.ClientId));

        InjectDock(scalar);
        EksenScalarScriptBuilder.Inject(
            scalar,
            resourceFileName: "docs-auth.js",
            configGlobalName: "__eksenScalarDocsAuthConfig",
            config: new
            {
                authorizeUrl = options.AuthorizeEndpoint,
                tokenUrl = options.TokenEndpoint,
                endSessionUrl = options.EndSessionEndpoint,
                clientId = options.ClientId,
                scope = options.Scope,
                callbackPath = options.CallbackPath,
                channel = OAuthChannel,
                documentPathPrefix = options.DocumentPathPrefix,
                apiPathPrefixes = options.ApiPathPrefixes,
                storageKey = options.StorageKey,
                requiredClaim = options.RequiredClaim is null
                    ? null
                    : new { name = options.RequiredClaim.Name, values = options.RequiredClaim.Values },
                title = options.Title,
                subtitle = options.Subtitle,
                eyebrow = options.Eyebrow,
                logoUrl = options.LogoUrl,
                logoAltText = options.LogoAltText,
                text = options.Text
            });

        return scalar;
    }

    /// <summary>
    /// Applies brand tokens: Scalar's built-in theme is switched off and replaced with CSS generated from
    /// <see cref="ScalarThemeOptions"/>. Without a dark palette the reference is pinned to light mode.
    /// </summary>
    public static ScalarOptions WithEksenTheme(
        this ScalarOptions scalar,
        Action<ScalarThemeOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(scalar);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new ScalarThemeOptions();
        configure(options);

        scalar.Theme = ScalarTheme.None;
        scalar.DefaultFonts = options.FontFaces.Count == 0 && options.BodyFont is null;
        scalar.CustomCss = (scalar.CustomCss is null ? string.Empty : scalar.CustomCss + "\n") + ScalarThemeCss.Build(options);

        if (options.Dark is null)
        {
            scalar.ForceThemeMode = ThemeMode.Light;
            scalar.HideDarkModeToggle = true;
        }

        return scalar;
    }

    /// <summary>
    /// The default path of the docs sign-in popup's landing page.
    /// </summary>
    public const string DefaultOAuthCallbackPath = "/scalar-auth/callback";

    internal const string OAuthChannel = "eksen-docs-oauth";

    /// <summary>
    /// Maps the landing page the provider redirects the docs sign-in popup to. It hands the authorization code
    /// back to the reference page and closes itself. Register its absolute URL as a redirect URI of the client.
    /// </summary>
    public static IEndpointConventionBuilder MapEksenScalarOAuthCallback(
        this IEndpointRouteBuilder endpoints,
        string pattern = DefaultOAuthCallbackPath)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        return endpoints.MapGet(pattern, (HttpContext context) =>
            {
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers["Referrer-Policy"] = "no-referrer";
                return Results.Content(ScalarPluginResources.Read("oauth-callback.html"), "text/html; charset=utf-8");
            })
            .ExcludeFromDescription()
            .AllowAnonymous();
    }

    private static void InjectDock(ScalarOptions scalar)
    {
        if (scalar.HeadContent?.Contains(DockMarker, StringComparison.Ordinal) == true)
        {
            return;
        }

        EksenScalarScriptBuilder.Inject(scalar, resourceFileName: "dock.js");
    }

    private const string DockMarker = "window.__eksenScalarDock";

    /// <summary>
    /// Adds the internal-auth wall plugin that gates the page behind an in-page e-mail and password card
    /// (resource-owner password grant) only host users can pass. Prefer <see cref="WithEksenDocsAuthPlugin"/>
    /// for providers that sign users in with the authorization-code flow, such as Keycloak.
    /// </summary>
    public static ScalarOptions WithEksenInternalAuthPlugin(
        this ScalarOptions scalar,
        Action<ScalarInternalAuthOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(scalar);

        var options = new ScalarInternalAuthOptions();
        configure?.Invoke(options);

        EksenScalarScriptBuilder.Inject(
            scalar,
            resourceFileName: "internal-auth.js",
            configGlobalName: "__eksenScalarInternalAuthConfig",
            config: new
            {
                clientId = options.ClientId,
                clientSecret = options.ClientSecret,
                username = options.DefaultUsername,
                tokenUrl = options.TokenEndpoint,
                internalDocPath = options.InternalDocumentPath,
                hostClaim = options.HostClaim,
                brandText = options.BrandText,
                subtitleText = options.SubtitleText
            });

        return scalar;
    }
}
