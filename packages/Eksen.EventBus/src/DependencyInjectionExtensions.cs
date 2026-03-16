using System.Reflection;
using Eksen.Core;
using Eksen.EventBus;
using Eksen.EventBus.BackgroundServices;
using Eksen.EventBus.Retry;
using Microsoft.Extensions.DependencyInjection.Extensions;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;

public interface IEksenEventBusBuilder
{
    IEksenBuilder EksenBuilder { get; }

    IEksenEventBusBuilder Subscribe<TEvent, THandler>()
        where TEvent : class, IIntegrationEvent
        where THandler : class, IEventHandler<TEvent>;

    IEksenEventBusBuilder SubscribeFromAssembly(Assembly assembly);

    IEksenEventBusBuilder Configure(Action<EksenEventBusOptions> configureOptions);
}

public class EksenEventBusBuilder(IEksenBuilder eksenBuilder) : IEksenEventBusBuilder
{
    private readonly EventHandlerRegistry _registry = new();

    public IEksenBuilder EksenBuilder { get; } = eksenBuilder;

    internal EventHandlerRegistry Registry => _registry;

    public IEksenEventBusBuilder Subscribe<TEvent, THandler>()
        where TEvent : class, IIntegrationEvent
        where THandler : class, IEventHandler<TEvent>
    {
        _registry.Register<TEvent, THandler>();
        EksenBuilder.Services.TryAddScoped<THandler>();
        return this;
    }

    public IEksenEventBusBuilder SubscribeFromAssembly(Assembly assembly)
    {
        var handlerTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>))
                .Select(i => new { HandlerType = t, EventType = i.GetGenericArguments()[0] }));

        foreach (var pair in handlerTypes)
        {
            _registry.Register(pair.EventType, pair.HandlerType);
            EksenBuilder.Services.TryAddScoped(pair.HandlerType);
        }

        return this;
    }

    public IEksenEventBusBuilder Configure(Action<EksenEventBusOptions> configureOptions)
    {
        EksenBuilder.Services.Configure(configureOptions);
        return this;
    }
}

public static class DependencyInjectionExtensions
{
    public static IEksenBuilder AddEventBus(
        this IEksenBuilder builder,
        Action<IEksenEventBusBuilder>? configureAction = null)
    {
        var services = builder.Services;

        var eventBusBuilder = new EksenEventBusBuilder(builder);

        services.TryAddSingleton<IEventSerializer, JsonEventSerializer>();
        services.TryAddSingleton<IEventRetryPipelineProvider, EventRetryPipelineProvider>();
        services.TryAddScoped<IEventProcessor, EventProcessor>();
        services.TryAddScoped<IEventBus, DefaultEventBus>();

        configureAction?.Invoke(eventBusBuilder);

        services.AddSingleton<IEventHandlerRegistry>(eventBusBuilder.Registry);
        services.AddHostedService<OutboxProcessorBackgroundService>();
        services.AddHostedService<EventBusListenerBackgroundService>();

        return builder;
    }

    public static IEksenEventBusBuilder UseUnitOfWork(this IEksenEventBusBuilder builder)
    {
        var services = builder.EksenBuilder.Services;

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IEventBus));
        if (descriptor != null)
        {
            services.Remove(descriptor);
        }

        services.TryAddScoped<DefaultEventBus>();
        services.AddScoped<IEventBus, UnitOfWorkEventBus>();

        return builder;
    }
}
