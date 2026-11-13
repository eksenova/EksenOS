using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Eksen.Telemetry.Redaction;

/// <summary>
/// Structured log fields (a log scope or event state) whose values are redacted as they are set, with
/// <see cref="TelemetryValueRedactor.RedactValue"/>. Redacting log pipelines pass these fields through as they are
/// instead of redacting them again for every record written inside the scope.
/// </summary>
public sealed class RedactedLogFields : IReadOnlyDictionary<string, object?>
{
    private readonly Dictionary<string, object?> _fields = new(StringComparer.Ordinal);

    public int Count
    {
        get { return _fields.Count; }
    }

    public object? this[string key]
    {
        get { return _fields[key]; }
    }

    public IEnumerable<string> Keys
    {
        get { return _fields.Keys; }
    }

    public IEnumerable<object?> Values
    {
        get { return _fields.Values; }
    }

    /// <summary>
    /// Sets <paramref name="name"/> to the redacted <paramref name="value"/>.
    /// </summary>
    public void Set(string name, object? value)
    {
        ArgumentNullException.ThrowIfNull(name);

        _fields[name] = TelemetryValueRedactor.RedactValue(name, value);
    }

    public bool ContainsKey(string key)
    {
        return _fields.ContainsKey(key);
    }

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out object? value)
    {
        return _fields.TryGetValue(key, out value);
    }

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        return _fields.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
