using System.Net.Http;
using Microsoft.Extensions.Options;

namespace Eksen.Cloudflare;

public sealed class HttpCloudflareIpRangeSource(
    IHttpClientFactory httpClientFactory,
    IOptions<EksenCloudflareOptions> options
) : ICloudflareIpRangeSource
{
    public async Task<CloudflareIpRanges> FetchAsync(CancellationToken cancellationToken = default)
    {
        var value = options.Value;

        using var httpClient = httpClientFactory.CreateClient(CloudflareDefaults.HttpClientName);

        using var timeoutSource = new CancellationTokenSource(value.FetchTimeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);

        var ipV4Lines = await ReadListAsync(httpClient, value.IpV4ListUrl, linkedSource.Token);
        var ipV6Lines = await ReadListAsync(httpClient, value.IpV6ListUrl, linkedSource.Token);

        var ranges = CloudflareIpRanges.Parse([.. ipV4Lines, .. ipV6Lines, .. value.AdditionalTrustedNetworks]);

        if (ranges.Networks.Length < value.MinimumExpectedNetworks)
        {
            throw new CloudflareIpRangeException(
                $"Cloudflare returned {ranges.Networks.Length} usable networks, "
                + $"which is below the configured minimum of {value.MinimumExpectedNetworks}.");
        }

        return ranges;
    }

    private static async Task<string[]> ReadListAsync(
        HttpClient httpClient,
        string requestUri,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        return content.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
