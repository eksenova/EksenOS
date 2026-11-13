using System.Text.RegularExpressions;

namespace Eksen.Telemetry.Redaction;

/// <summary>
/// Removes secrets that travel in URL paths (for example a licence key used as a path segment) before the
/// path reaches a span, metric or log record.
/// </summary>
public interface IUrlPathScrubber
{
    string Scrub(string path);
}

/// <summary>
/// One path rewrite, applied with <see cref="Regex.Replace(string, string)"/>.
/// </summary>
public sealed record UrlPathScrubRule(Regex Pattern, string Replacement);

/// <summary>
/// Applies every registered <see cref="UrlPathScrubRule"/> in registration order.
/// </summary>
public sealed class DefaultUrlPathScrubber(IEnumerable<UrlPathScrubRule> rules) : IUrlPathScrubber
{
    private readonly UrlPathScrubRule[] _rules = rules.ToArray();

    public string Scrub(string path)
    {
        if (string.IsNullOrEmpty(path) || _rules.Length == 0)
        {
            return path;
        }

        var scrubbed = path;
        foreach (var rule in _rules)
        {
            try
            {
                scrubbed = rule.Pattern.Replace(scrubbed, rule.Replacement);
            }
            catch (RegexMatchTimeoutException)
            {
                return "[unscrubbable path]";
            }
        }

        return scrubbed;
    }
}

public static class UrlPathScrubberExtensions
{
    /// <summary>
    /// Returns <paramref name="uri"/> with its path scrubbed and its sensitive query values redacted.
    /// </summary>
    public static string ScrubUrl(this IUrlPathScrubber scrubber, Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!uri.IsAbsoluteUri)
        {
            return ScrubUrl(scrubber, uri.OriginalString);
        }

        // Uri.Authority leaves out user info, so credentials embedded in the URL are dropped.
        var authority = uri.Scheme + "://" + uri.Authority;
        var path = scrubber.Scrub(uri.AbsolutePath);
        var query = uri.Query;

        return SensitiveUrlRedactor.Redact(authority + path + query);
    }

    /// <summary>
    /// Scrubs a URL given as text: an absolute URL, or a path with an optional query string.
    /// </summary>
    public static string ScrubUrl(this IUrlPathScrubber scrubber, string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return url;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute)
            && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
        {
            return ScrubUrl(scrubber, absolute);
        }

        var queryStart = url.IndexOfAny(['?', '#']);
        var path = queryStart >= 0 ? url[..queryStart] : url;
        var rest = queryStart >= 0 ? url[queryStart..] : string.Empty;

        return SensitiveUrlRedactor.Redact(scrubber.Scrub(path) + rest);
    }
}
