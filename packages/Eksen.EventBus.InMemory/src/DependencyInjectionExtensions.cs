using Eksen.EventBus;
using Eksen.EventBus.DeadLetter;
using Eksen.EventBus.Inbox;
using Eksen.EventBus.InMemory;
using Eksen.EventBus.Outbox;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;

public static class InMemoryDependencyInjectionExtensions
{
    public static IEksenEventBusBuilder UseInMemory(this IEksenEventBusBuilder builder)
    {
        var services = builder.EksenBuilder.Services;

        services.AddSingleton<InMemoryEventBusTransport>();
        services.AddSingleton<IEventBusTransport>(sp => sp.GetRequiredService<InMemoryEventBusTransport>());
        services.AddSingleton<IOutboxStore, InMemoryOutboxStore>();
        services.AddSingleton<IInboxStore, InMemoryInboxStore>();
        services.AddSingleton<IDeadLetterStore, InMemoryDeadLetterStore>();

        return builder;
    }
}
