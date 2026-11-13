using Eksen.EventBus.Inbox;
using Eksen.EventBus.Redis;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;

public static class RedisDependencyInjectionExtensions
{
    /// <summary>
    /// Uses Redis as the consumer inbox: a handler claims an event atomically (<c>SET NX</c> with
    /// <see cref="RedisInboxOptions.ProcessingTimeout"/>), and the processed marker is kept for
    /// <c>InboxOptions.IdempotencyWindow</c>. Replaces any inbox store registered before. Needs no database or unit of
    /// work, so a consumer-only host can call <c>AddEventBus</c>, <c>UseRabbitMq</c> and <c>UseRedisInbox</c> alone.
    /// </summary>
    public static IEksenEventBusBuilder UseRedisInbox(
        this IEksenEventBusBuilder builder,
        Action<RedisInboxOptions>? configureOptions = null)
    {
        var services = builder.EksenBuilder.Services;

        var optionsBuilder = services.AddOptions<RedisInboxOptions>();
        if (configureOptions != null)
        {
            optionsBuilder.Configure(configureOptions);
        }

        services.TryAddSingleton<IConnectionMultiplexer>(serviceProvider =>
        {
            var redisOptions = serviceProvider.GetRequiredService<IOptions<RedisInboxOptions>>().Value;
            if (string.IsNullOrWhiteSpace(redisOptions.Configuration))
            {
                throw new InvalidOperationException(
                    "No IConnectionMultiplexer is registered and RedisInboxOptions.Configuration is not set.");
            }

            var configuration = ConfigurationOptions.Parse(redisOptions.Configuration);
            configuration.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(configuration);
        });

        services.RemoveAll<IInboxStore>();
        services.AddSingleton<RedisInboxStore>();
        services.AddSingleton<IInboxStore>(serviceProvider => serviceProvider.GetRequiredService<RedisInboxStore>());

        return builder;
    }
}
