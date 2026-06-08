namespace Eksen.Scalar;

/// <summary>
/// Options for the token-body plugin, which rewrites token requests so Basic client
/// credentials are moved into the form body (<c>client_id</c> / <c>client_secret</c>).
/// Passed to <see cref="EksenScalarExtensions.WithEksenTokenBodyPlugin"/>.
/// </summary>
public sealed class ScalarTokenBodyOptions
{
    /// <summary>
    /// The token endpoint path matched by the plugin. Defaults to <c>/connect/token</c>.
    /// </summary>
    public string TokenEndpoint { get; set; } = "/connect/token";
}
