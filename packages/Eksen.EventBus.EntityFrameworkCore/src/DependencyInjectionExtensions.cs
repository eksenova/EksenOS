using Eksen.EventBus;
using Eksen.EventBus.DeadLetter;
using Eksen.EventBus.EntityFrameworkCore;
using Eksen.EventBus.Inbox;
using Eksen.EventBus.Outbox;
using Microsoft.EntityFrameworkCore;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;

public static class EfCoreDependencyInjectionExtensions
{
    public static IEksenEventBusBuilder UseEntityFrameworkCore(
        this IEksenEventBusBuilder builder,
        Action<DbContextOptionsBuilder>? configureDbContext = null)
    {
        var services = builder.EksenBuilder.Services;

        services.AddDbContext<EventBusDbContext>(configureDbContext ?? (_ => { }));
        services.AddScoped<IOutboxStore, EfCoreOutboxStore>();
        services.AddScoped<IInboxStore, EfCoreInboxStore>();
        services.AddScoped<IDeadLetterStore, EfCoreDeadLetterStore>();

        return builder;
    }
}
