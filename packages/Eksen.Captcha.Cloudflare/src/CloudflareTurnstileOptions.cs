using System.ComponentModel.DataAnnotations;
using Eksen.Cloudflare;

namespace Eksen.Captcha.Cloudflare;

public sealed record CloudflareTurnstileOptions
{
    public const string DefaultConfigSectionPath = "Eksen:Captcha:Cloudflare";

    [Required]
    public string SecretKey { get; set; } = string.Empty;

    public string? SiteKey { get; set; }

    [Required]
    [Url]
    public string VerifyUrl { get; set; } = CloudflareDefaults.TurnstileVerifyUrl;

    public TimeSpan VerifyTimeout { get; set; } = TimeSpan.FromSeconds(10);

    public bool SendRemoteIpAddress { get; set; } = true;
}
