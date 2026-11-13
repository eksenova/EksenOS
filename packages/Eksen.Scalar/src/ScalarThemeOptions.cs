namespace Eksen.Scalar;

/// <summary>
/// Brand tokens for <see cref="EksenScalarExtensions.WithEksenTheme"/>. The plugin turns them into Scalar's CSS
/// custom properties, so the reference, the Eksen plugins and the sign-in card all read from one palette.
/// </summary>
public sealed class ScalarThemeOptions
{
    /// <summary>
    /// Colors for light mode.
    /// </summary>
    public ScalarPalette Light { get; set; } = new();

    /// <summary>
    /// Colors for dark mode. When <see langword="null"/>, the reference is pinned to light mode and the mode
    /// toggle is hidden.
    /// </summary>
    public ScalarPalette? Dark { get; set; }

    /// <summary>
    /// Body font stack, for example <c>"Source Sans 3", sans-serif</c>.
    /// </summary>
    public string? BodyFont { get; set; }

    /// <summary>
    /// Heading font stack used for the reference's headings and the plugins' titles.
    /// </summary>
    public string? DisplayFont { get; set; }

    /// <summary>
    /// Monospace font stack for code.
    /// </summary>
    public string? CodeFont { get; set; }

    /// <summary>
    /// Fonts to load with <c>@font-face</c>. Serve the files from the application.
    /// </summary>
    public IList<ScalarFontFace> FontFaces { get; } = [];

    /// <summary>
    /// Base corner radius; Scalar derives its larger radii from it. For example <c>6px</c>.
    /// </summary>
    public string? Radius { get; set; }

    /// <summary>
    /// Shadow for floating panels such as the impersonation picker. Every other surface stays flat.
    /// Defaults to <c>none</c>.
    /// </summary>
    public string OverlayShadow { get; set; } = "none";

    /// <summary>
    /// Background of the brand band behind the sidebar logo and the sign-in card's logo. Defaults to the accent.
    /// </summary>
    public string? BrandBackground { get; set; }

    /// <summary>
    /// Height of the brand band. Defaults to <c>56px</c>.
    /// </summary>
    public string BrandHeight { get; set; } = "56px";

    /// <summary>
    /// Height of the logo inside the brand band. Defaults to <c>24px</c>.
    /// </summary>
    public string BrandLogoHeight { get; set; } = "24px";

    /// <summary>
    /// Extra CSS appended after the generated rules.
    /// </summary>
    public string? AdditionalCss { get; set; }
}

/// <summary>
/// One color mode of a <see cref="ScalarThemeOptions"/>.
/// </summary>
public sealed class ScalarPalette
{
    /// <summary>Primary text.</summary>
    public string Text { get; set; } = "#212121";

    /// <summary>Secondary text such as descriptions.</summary>
    public string TextSecondary { get; set; } = "#616161";

    /// <summary>Muted text such as placeholders.</summary>
    public string TextMuted { get; set; } = "#757575";

    /// <summary>Links, focus rings and primary actions.</summary>
    public string Accent { get; set; } = "#5e35b1";

    /// <summary>Primary action on hover.</summary>
    public string AccentHover { get; set; } = "#512da8";

    /// <summary>Text on an accent fill.</summary>
    public string OnAccent { get; set; } = "#ffffff";

    /// <summary>Tinted accent fill for selected and hovered rows.</summary>
    public string AccentTint { get; set; } = "#ede7f6";

    /// <summary>Working surface: content, cards, inputs.</summary>
    public string Surface { get; set; } = "#ffffff";

    /// <summary>Page and sidebar background behind surfaces.</summary>
    public string Canvas { get; set; } = "#f5f5f5";

    /// <summary>Strongest neutral fill, for example code blocks.</summary>
    public string SurfaceStrong { get; set; } = "#eeeeee";

    /// <summary>Separators and quiet outlines.</summary>
    public string Border { get; set; } = "#e0e0e0";

    /// <summary>Outlines of form controls.</summary>
    public string ControlBorder { get; set; } = "#757575";

    /// <summary>Errors and destructive actions.</summary>
    public string Danger { get; set; } = "#d50000";

    /// <summary>Background behind error messages.</summary>
    public string DangerTint { get; set; } = "#ffebee";
}

/// <summary>
/// One <c>@font-face</c> rule.
/// </summary>
/// <param name="Family">Font family name.</param>
/// <param name="Url">URL of a WOFF2 file.</param>
/// <param name="Weight">CSS font weight, for example <c>400</c>.</param>
/// <param name="UnicodeRange">Optional unicode range for subset files.</param>
public sealed record ScalarFontFace(string Family, string Url, int Weight, string? UnicodeRange = null);
