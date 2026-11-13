using System.Text;

namespace Eksen.Scalar.Plugins;

/// <summary>
/// Builds the custom CSS for <see cref="EksenScalarExtensions.WithEksenTheme"/> from brand tokens.
/// </summary>
internal static class ScalarThemeCss
{
    public static string Build(ScalarThemeOptions options)
    {
        var css = new StringBuilder();

        foreach (var face in options.FontFaces)
        {
            css.Append("@font-face{font-family:").Append(Quote(face.Family))
                .Append(";font-style:normal;font-display:swap;font-weight:").Append(face.Weight)
                .Append(";src:url(").Append(Quote(face.Url)).Append(") format('woff2');");
            if (!string.IsNullOrWhiteSpace(face.UnicodeRange))
            {
                css.Append("unicode-range:").Append(face.UnicodeRange).Append(';');
            }

            css.Append("}\n");
        }

        css.Append(":root{");
        Append(css, "--scalar-font", options.BodyFont);
        Append(css, "--scalar-font-code", options.CodeFont);
        Append(css, "--eks-display-font", options.DisplayFont ?? options.BodyFont);
        Append(css, "--scalar-radius", options.Radius);
        Append(css, "--scalar-radius-lg", options.Radius);
        Append(css, "--scalar-shadow-1", "none");
        Append(css, "--scalar-shadow-2", "none");
        Append(css, "--eks-overlay-shadow", options.OverlayShadow);
        Append(css, "--eks-brand-height", options.BrandHeight);
        Append(css, "--eks-brand-logo-height", options.BrandLogoHeight);
        Append(css, "--eks-brand-padding", "0 20px");
        css.Append("}\n");

        AppendPalette(css, ".light-mode", options.Light, options.BrandBackground);
        if (options.Dark is not null)
        {
            AppendPalette(css, ".dark-mode", options.Dark, options.BrandBackground);
        }

        if (options.DisplayFont is not null)
        {
            css.Append(".scalar-app h1,.scalar-app h2,.scalar-app .section-header,.scalar-app .section-header-label{font-family:var(--eks-display-font);}\n");
        }

        if (!string.IsNullOrWhiteSpace(options.AdditionalCss))
        {
            css.Append(options.AdditionalCss).Append('\n');
        }

        return css.ToString();
    }

    private static void AppendPalette(StringBuilder css, string selector, ScalarPalette palette, string? brandBackground)
    {
        css.Append(selector).Append('{');
        Append(css, "--scalar-color-1", palette.Text);
        Append(css, "--scalar-color-2", palette.TextSecondary);
        Append(css, "--scalar-color-3", palette.TextMuted);
        Append(css, "--scalar-color-accent", palette.Accent);
        Append(css, "--scalar-background-1", palette.Surface);
        Append(css, "--scalar-background-2", palette.Canvas);
        Append(css, "--scalar-background-3", palette.SurfaceStrong);
        Append(css, "--scalar-background-accent", palette.AccentTint);
        Append(css, "--scalar-border-color", palette.Border);
        Append(css, "--scalar-button-1", palette.Accent);
        Append(css, "--scalar-button-1-hover", palette.AccentHover);
        Append(css, "--scalar-button-1-color", palette.OnAccent);
        Append(css, "--scalar-color-red", palette.Danger);
        Append(css, "--scalar-color-danger", palette.Danger);
        Append(css, "--scalar-background-danger", palette.DangerTint);
        Append(css, "--eks-control-line", palette.ControlBorder);
        Append(css, "--eks-wall-background", palette.Canvas);
        Append(css, "--eks-brand-background", brandBackground ?? palette.Accent);
        Append(css, "--eks-brand-border", "transparent");
        css.Append("}\n");

        css.Append(selector).Append(" .sidebar,").Append(selector).Append(" .t-doc__sidebar{");
        Append(css, "--scalar-sidebar-background-1", palette.Canvas);
        Append(css, "--scalar-sidebar-color-1", palette.Text);
        Append(css, "--scalar-sidebar-color-2", palette.TextSecondary);
        Append(css, "--scalar-sidebar-border-color", palette.Border);
        Append(css, "--scalar-sidebar-item-hover-background", palette.SurfaceStrong);
        Append(css, "--scalar-sidebar-item-hover-color", palette.Text);
        Append(css, "--scalar-sidebar-item-active-background", palette.AccentTint);
        Append(css, "--scalar-sidebar-color-active", palette.Accent);
        Append(css, "--scalar-sidebar-search-background", palette.Surface);
        Append(css, "--scalar-sidebar-search-border-color", palette.ControlBorder);
        Append(css, "--scalar-sidebar-search-color", palette.TextSecondary);
        css.Append("}\n");
    }

    private static void Append(StringBuilder css, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (value.Contains(';') || value.Contains('}') || value.Contains('{') || value.Contains('<'))
        {
            throw new ArgumentException($"Theme value for {name} contains characters that are not allowed in a CSS value.", nameof(value));
        }

        css.Append(name).Append(':').Append(value).Append(';');
    }

    private static string Quote(string value)
    {
        if (value.Contains('\'') || value.Contains('\\') || value.Contains('<'))
        {
            throw new ArgumentException("Font names and URLs must not contain quotes, backslashes or '<'.", nameof(value));
        }

        return "'" + value + "'";
    }
}
