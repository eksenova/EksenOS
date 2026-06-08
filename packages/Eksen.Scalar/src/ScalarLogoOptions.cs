namespace Eksen.Scalar;

/// <summary>
/// Branding options for the Scalar sidebar logo and footer copyright line, passed to
/// <see cref="EksenScalarExtensions.WithEksenLogoPlugin"/>.
/// </summary>
public sealed class ScalarLogoOptions
{
    /// <summary>
    /// URL of the logo image rendered in the sidebar. May be an absolute URL or an app-relative path.
    /// When <see langword="null"/>, no logo image is injected.
    /// </summary>
    public string? LogoUrl { get; set; }

    /// <summary>
    /// Alt text / accessible name for the logo image.
    /// </summary>
    public string LogoAltText { get; set; } = "Logo";

    /// <summary>
    /// Optional footer text rendered as a fixed copyright badge. When <see langword="null"/>, no footer is shown.
    /// The literal <c>{year}</c> is replaced with the current year at render time.
    /// </summary>
    public string? FooterText { get; set; }

    /// <summary>
    /// Optional tooltip / title for the footer badge. The literal <c>{year}</c> is replaced with the current year.
    /// </summary>
    public string? FooterTitle { get; set; }

    /// <summary>
    /// When <see langword="true"/>, sidebar entries that advertise MCP server generation are hidden.
    /// </summary>
    public bool HideMcpControls { get; set; } = true;

    /// <summary>
    /// When <see langword="true"/>, top-level sidebar categories are collapsed on first load.
    /// </summary>
    public bool CollapseSidebarCategories { get; set; } = true;
}
