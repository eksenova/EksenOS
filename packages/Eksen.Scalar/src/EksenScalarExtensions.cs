using Eksen.Scalar.Plugins;
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
    /// Adds the tenant impersonation plugin that lets host users exchange their bearer token for a
    /// tenant-scoped token and have it injected into subsequent API calls.
    /// </summary>
    public static ScalarOptions WithEksenImpersonationPlugin(
        this ScalarOptions scalar,
        Action<ScalarImpersonationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(scalar);

        var options = new ScalarImpersonationOptions();
        configure?.Invoke(options);

        EksenScalarScriptBuilder.Inject(
            scalar,
            resourceFileName: "impersonation.js",
            configGlobalName: "__eksenScalarImpersonationConfig",
            config: new
            {
                tokenEndpoint = options.TokenEndpoint,
                tenantsEndpoint = options.TenantsEndpoint,
                grantType = options.GrantType,
                clientId = options.ClientId,
                hostClaim = options.HostClaim,
                impersonatingClaim = options.ImpersonatingClaim
            });

        return scalar;
    }

    /// <summary>
    /// Adds the internal-auth wall plugin that gates the page behind an in-page sign-in card only
    /// host users can pass. Map the internal Scalar reference (route, title, documents) separately.
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
