using System.ComponentModel.DataAnnotations;

namespace Eksen.Cloudflare;

public sealed record EksenCloudflareOptions
{
    public const string DefaultConfigSectionPath = "Eksen:Cloudflare";

    [Required]
    [Url]
    public string IpV4ListUrl { get; set; } = CloudflareDefaults.IpV4ListUrl;

    [Required]
    [Url]
    public string IpV6ListUrl { get; set; } = CloudflareDefaults.IpV6ListUrl;

    [Required]
    public string ConnectingIpHeaderName { get; set; } = CloudflareDefaults.ConnectingIpHeaderName;

    [Required]
    public string ForwardedProtoHeaderName { get; set; } = CloudflareDefaults.ForwardedProtoHeaderName;

    public bool RestoreConnectingIp { get; set; } = true;

    public bool RestoreForwardedProto { get; set; } = true;

    [Range(minimum: 1, maximum: int.MaxValue)]
    public int MaxFetchAttempts { get; set; } = 3;

    public TimeSpan FetchRetryDelay { get; set; } = TimeSpan.FromSeconds(2);

    public TimeSpan? UncachedRetryTimeout { get; set; }

    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromHours(12);

    public TimeSpan FetchTimeout { get; set; } = TimeSpan.FromSeconds(15);

    [Range(minimum: 1, maximum: int.MaxValue)]
    public int MinimumExpectedNetworks { get; set; } = 10;

    public IList<string> AdditionalTrustedNetworks { get; set; } = [];
}
