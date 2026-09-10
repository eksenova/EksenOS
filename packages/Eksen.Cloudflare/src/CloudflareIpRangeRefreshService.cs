using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eksen.Cloudflare;

public sealed class CloudflareIpRangeRefreshService(
    ICloudflareIpRangeProvider rangeProvider,
    IOptions<EksenCloudflareOptions> options,
    ILogger<CloudflareIpRangeRefreshService> logger,
    TimeProvider timeProvider
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await rangeProvider.RefreshAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Refreshing Cloudflare IP ranges failed unexpectedly.");
            }

            try
            {
                await Task.Delay(options.Value.RefreshInterval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
