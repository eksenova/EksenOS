using System.Text.Encodings.Web;
using System.Text.Json;
using Scalar.AspNetCore;

namespace Eksen.Scalar.Plugins;

/// <summary>
/// Appends an Eksen client-side plugin to a Scalar reference as an inline ES-module
/// <c>&lt;script&gt;</c> on <see cref="ScalarOptions.HeadContent"/>. Each plugin's JavaScript ships as
/// an embedded <c>Resources/*.js</c> module; its strongly-typed configuration is serialized and
/// assigned to a <c>window</c> global the module reads on load. Injecting into the document head
/// avoids serving any extra HTTP endpoint, so the plugins compose by simply being appended in turn.
/// </summary>
internal static class EksenScalarScriptBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Injects the embedded plugin module named <paramref name="resourceFileName"/>, optionally
    /// prefixed with an assignment of <paramref name="config"/> to the <paramref name="configGlobalName"/>
    /// <c>window</c> property when the plugin takes configuration.
    /// </summary>
    public static void Inject(
        ScalarOptions scalar,
        string resourceFileName,
        string? configGlobalName = null,
        object? config = null)
    {
        var source = ScalarPluginResources.Read(resourceFileName);

        var js = configGlobalName is not null
            ? "window." + configGlobalName + " = " + JsonSerializer.Serialize(config, JsonOptions) + ";\n" + source
            : source;

        Append(scalar, js);
    }

    private static void Append(ScalarOptions scalar, string js)
    {
        // The module body is embedded verbatim in HTML, so neutralize any literal that would close
        // the surrounding <script> element early; the escaped form is equivalent inside JavaScript.
        var safe = js.Replace("</script", "<\\/script", StringComparison.OrdinalIgnoreCase);

        var tag = "<script type=\"module\">\n" + safe + "\n</script>";

        scalar.HeadContent = string.IsNullOrEmpty(scalar.HeadContent)
            ? tag
            : scalar.HeadContent + "\n" + tag;
    }
}
