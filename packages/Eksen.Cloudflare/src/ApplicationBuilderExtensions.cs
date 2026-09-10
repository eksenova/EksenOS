using Eksen.Cloudflare;

#pragma warning disable IDE0130

namespace Microsoft.AspNetCore.Builder;

public static class CloudflareApplicationBuilderExtensions
{
    public static IApplicationBuilder UseCloudflare(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<CloudflareMiddleware>();
    }
}
