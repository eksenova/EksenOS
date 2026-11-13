using Microsoft.AspNetCore.Http;

namespace Eksen.Telemetry.AspNetCore.Enrichment;

/// <summary>
/// A field added to the request's log scope and completion event; also set as a span tag when
/// <paramref name="ActivityTag"/> is <see langword="true"/>.
/// </summary>
public sealed record RequestEnrichmentProperty(string Name, object? Value, bool ActivityTag = true);

/// <summary>
/// Application hook that contributes request fields (for example the authenticated user id). Contributors run
/// after authentication and again when the completion event is written; they must only emit identifiers, never
/// e-mail addresses, phone numbers or names.
/// </summary>
public interface IRequestEnrichmentContributor
{
    IEnumerable<RequestEnrichmentProperty> Collect(HttpContext context);
}

internal static class RequestEnrichmentValues
{
    public static bool IsEmpty(object? value)
    {
        return value is null || (value is string text && text.Length == 0);
    }

    public static string? NullIfEmpty(this string? value)
    {
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
