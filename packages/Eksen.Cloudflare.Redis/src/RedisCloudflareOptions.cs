using System.ComponentModel.DataAnnotations;

namespace Eksen.Cloudflare.Redis;

public sealed record RedisCloudflareOptions
{
    public const string DefaultConfigSectionPath = "Eksen:Cloudflare:Redis";

    public const string DefaultKey = "eksen:cloudflare:ip-ranges";

    public string? ConnectionString { get; set; }

    [Required]
    public string Key { get; set; } = DefaultKey;

    public TimeSpan? Expiry { get; set; }
}
