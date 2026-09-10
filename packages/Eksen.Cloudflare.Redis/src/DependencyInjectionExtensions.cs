using Eksen.Cloudflare;
using Eksen.Cloudflare.Redis;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

#pragma warning disable IDE0130

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    extension(IEksenCloudflareBuilder builder)
    {
        public IEksenCloudflareBuilder UseRedis(
            Action<RedisCloudflareOptions>? configureOptions = null,
            string configSectionPath = RedisCloudflareOptions.DefaultConfigSectionPath)
        {
            var services = builder.Services;

            var optionsBuilder = services
                .AddOptions<RedisCloudflareOptions>()
                .BindConfiguration(configSectionPath)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            if (configureOptions != null)
            {
                optionsBuilder.Configure(configureOptions);
            }

            services.TryAddSingleton<IConnectionMultiplexer>(serviceProvider =>
            {
                var redisOptions = serviceProvider
                    .GetRequiredService<IOptions<RedisCloudflareOptions>>()
                    .Value;

                if (string.IsNullOrWhiteSpace(redisOptions.ConnectionString))
                {
                    throw new InvalidOperationException(
                        "No IConnectionMultiplexer is registered and "
                        + $"{configSectionPath}:ConnectionString is not configured.");
                }

                return ConnectionMultiplexer.Connect(redisOptions.ConnectionString);
            });

            services.RemoveAll<ICloudflareIpRangeStore>();
            services.AddSingleton<ICloudflareIpRangeStore, RedisCloudflareIpRangeStore>();

            return builder;
        }
    }
}
