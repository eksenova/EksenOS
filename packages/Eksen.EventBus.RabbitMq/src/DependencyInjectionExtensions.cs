using Eksen.EventBus;
using Eksen.EventBus.RabbitMq;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;

public interface IEksenEventBusRabbitMqBuilder
{
    IEksenEventBusBuilder EventBusBuilder { get; }

    IEksenEventBusRabbitMqBuilder Configure(Action<RabbitMqEventBusOptions> configureOptions);

    IEksenEventBusRabbitMqBuilder MapEventToQueue<TEvent>(string queueName, string routingKey)
        where TEvent : class, IIntegrationEvent;

    IEksenEventBusRabbitMqBuilder ConfigureQueue(string queueName, Action<QueueOptions> configureQueue);
}

public class EksenEventBusRabbitMqBuilder(IEksenEventBusBuilder eventBusBuilder) : IEksenEventBusRabbitMqBuilder
{
    public IEksenEventBusBuilder EventBusBuilder { get; } = eventBusBuilder;

    public IEksenEventBusRabbitMqBuilder Configure(Action<RabbitMqEventBusOptions> configureOptions)
    {
        EventBusBuilder.EksenBuilder.Services.Configure(configureOptions);
        return this;
    }

    public IEksenEventBusRabbitMqBuilder MapEventToQueue<TEvent>(string queueName, string routingKey)
        where TEvent : class, IIntegrationEvent
    {
        var eventName = EventNameResolver.GetEventName<TEvent>();
        EventBusBuilder.EksenBuilder.Services.Configure<RabbitMqEventBusOptions>(o =>
        {
            o.EventQueueBindings[eventName] = new EventQueueBinding
            {
                QueueName = queueName,
                RoutingKey = routingKey
            };
        });
        return this;
    }

    public IEksenEventBusRabbitMqBuilder ConfigureQueue(string queueName, Action<QueueOptions> configureQueue)
    {
        EventBusBuilder.EksenBuilder.Services.Configure<RabbitMqEventBusOptions>(o =>
        {
            if (!o.QueueConfigurations.TryGetValue(queueName, out var queueOptions))
            {
                queueOptions = new QueueOptions();
                o.QueueConfigurations[queueName] = queueOptions;
            }

            configureQueue(queueOptions);
        });
        return this;
    }
}

public static class RabbitMqDependencyInjectionExtensions
{
    public static IEksenEventBusBuilder UseRabbitMq(
        this IEksenEventBusBuilder builder,
        Action<IEksenEventBusRabbitMqBuilder>? configureAction = null)
    {
        var services = builder.EksenBuilder.Services;

        services.AddSingleton<IRabbitMqConnectionManager, RabbitMqConnectionManager>();
        services.AddSingleton<RabbitMqEventBusTransport>();
        services.AddSingleton<IEventBusTransport>(sp => sp.GetRequiredService<RabbitMqEventBusTransport>());
        services.AddSingleton<IEventRescheduler, RabbitMqEventRescheduler>();

        if (configureAction != null)
        {
            var rabbitBuilder = new EksenEventBusRabbitMqBuilder(builder);
            configureAction(rabbitBuilder);
        }

        return builder;
    }
}
