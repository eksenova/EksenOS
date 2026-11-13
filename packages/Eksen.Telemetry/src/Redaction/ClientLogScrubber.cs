using System.Text.RegularExpressions;

namespace Eksen.Telemetry.Redaction;

/// <summary>
/// Cleans free text sent by untrusted clients (browser logs, error messages) before it is logged: drops URL
/// query strings and fragments, redacts JWTs, e-mail addresses, IBANs and long digit runs, removes control
/// characters and bounds the length.
/// </summary>
public static partial class ClientLogScrubber
{
    private const string Redacted = "[redacted]";

    public static string? Clean(string? value, int maxLength, bool keepLines = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var bounded = value.Length > maxLength * 2 ? value[..(maxLength * 2)] : value;
        var cleaned = UrlSuffix().Replace(bounded, "$1");
        cleaned = Jwt().Replace(cleaned, Redacted);
        cleaned = Email().Replace(cleaned, Redacted);
        cleaned = Iban().Replace(cleaned, Redacted);
        cleaned = LongNumber().Replace(cleaned, Redacted);
        cleaned = keepLines ? ControlExceptLines().Replace(cleaned, " ") : Control().Replace(cleaned, " ");
        cleaned = cleaned.Trim();

        return cleaned.Length > maxLength ? cleaned[..maxLength] : cleaned;
    }

    [GeneratedRegex(pattern: @"(https?://[^\s?#'""]+)[?#][^\s'""]*", RegexOptions.IgnoreCase)]
    private static partial Regex UrlSuffix();

    [GeneratedRegex(pattern: @"\beyJ[\w-]+\.[\w-]+\.[\w-]*")]
    private static partial Regex Jwt();

    // Starts only where the local part starts, so a long run without an @ is scanned once rather than from each
    // of its characters.
    [GeneratedRegex(pattern: @"(?<![\w.+-])(?>[\w.+-]+)@[\w-]+(\.[\w-]+)+")]
    private static partial Regex Email();

    [GeneratedRegex(pattern: @"\b[A-Z]{2}\d{2}(\s?[A-Z0-9]){11,30}\b")]
    private static partial Regex Iban();

    [GeneratedRegex(pattern: @"(?<![\d:.])\+?\d{10,}(?![\d:])")]
    private static partial Regex LongNumber();

    [GeneratedRegex(pattern: @"\p{Cc}+")]
    private static partial Regex Control();

    [GeneratedRegex(pattern: @"[\p{Cc}-[\n]]+")]
    private static partial Regex ControlExceptLines();
}
