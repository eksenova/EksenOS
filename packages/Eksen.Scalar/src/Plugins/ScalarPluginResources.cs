using System.Collections.Concurrent;
using System.Reflection;

namespace Eksen.Scalar.Plugins;

/// <summary>
/// Reads the client-side plugin sources that ship as embedded resources in this assembly.
/// Each plugin's JavaScript lives in its own <c>Resources/*.js</c> file rather than an inline
/// string literal; the content is loaded from the assembly manifest once and cached thereafter.
/// </summary>
internal static class ScalarPluginResources
{
    private const string ResourceNamespace = "Eksen.Scalar.Resources.";

    private static readonly Assembly Assembly = typeof(ScalarPluginResources).Assembly;

    private static readonly ConcurrentDictionary<string, string> Cache = new(StringComparer.Ordinal);

    /// <summary>
    /// Returns the content of the embedded plugin script with the given file name
    /// (for example <c>autofill.js</c>), reading it from the assembly manifest on first use.
    /// </summary>
    public static string Read(string fileName)
    {
        return Cache.GetOrAdd(fileName, static name =>
        {
            var resourceName = ResourceNamespace + name;

            using var stream = Assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException(
                    $"Embedded plugin resource '{resourceName}' was not found in assembly '{Assembly.GetName().Name}'.");

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        });
    }
}
