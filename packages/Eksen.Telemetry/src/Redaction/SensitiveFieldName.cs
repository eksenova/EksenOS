using System.Text.RegularExpressions;

namespace Eksen.Telemetry.Redaction;

/// <summary>
/// Recognises field, query and attribute names whose values are secrets.
/// </summary>
public static partial class SensitiveFieldName
{
    public const string Pattern =
        @"(?:[\w-]*(?:password|passwd|parola|sifre|şifre|secret|token|signature|credential)[\w-]*"
        + @"|pass|pwd|key|auth[_-]?data"
        + @"|[\w-]*(?:app|business|client|api|private|company|subscription|auth|licen[cs]e)[_-]?key"
        + @"|[\w-]*session[_-]?id)";

    /// <summary>
    /// A field name as the body redactor captures it before asking <see cref="IsSensitive"/>: one atomic run of
    /// word characters and hyphens, so a long name cannot make the surrounding pattern backtrack.
    /// </summary>
    internal const string NameToken = @"(?<name>(?>[\w-]+))";

    /// <summary>
    /// The match timeout of every redaction pattern. The patterns are linear, so this only bounds a defect; a value
    /// whose redaction times out is replaced entirely.
    /// </summary>
    internal const int RegexTimeoutMilliseconds = 250;

    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    [GeneratedRegex(pattern: "^" + Pattern + "$", Options, RegexTimeoutMilliseconds)]
    private static partial Regex ExactName();

    [GeneratedRegex(pattern: @"^[\w-]+$", Options, RegexTimeoutMilliseconds)]
    private static partial Regex WordName();

    public static bool IsSensitive(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        try
        {
            // Every alternative of the pattern consists of word characters and hyphens only. Checking that first
            // keeps the exact match linear: on a long name ending in another character, the leading and trailing
            // [\w-]* of the pattern would otherwise retry each other for every keyword occurrence.
            return WordName().IsMatch(name) && ExactName().IsMatch(name);
        }
        catch (RegexMatchTimeoutException)
        {
            return true;
        }
    }
}
