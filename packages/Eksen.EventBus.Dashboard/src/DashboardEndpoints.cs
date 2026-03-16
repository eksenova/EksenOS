using Eksen.EventBus.DeadLetter;
using Eksen.EventBus.Inbox;
using Eksen.EventBus.Outbox;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Eksen.EventBus.Dashboard;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapEventBusDashboardApi(
        this IEndpointRouteBuilder endpoints,
        string routePrefix)
    {
        var api = endpoints.MapGroup($"/{routePrefix}/api");

        api.MapGet("/stats", GetStatsAsync);
        api.MapGet("/outbox", GetOutboxMessagesAsync);
        api.MapGet("/inbox", GetInboxMessagesAsync);
        api.MapGet("/deadletter", GetDeadLetterMessagesAsync);
        api.MapPost("/deadletter/{id}/requeue", RequeueDeadLetterAsync);
        api.MapGet("/handlers", GetHandlersAsync);

        return endpoints;
    }

    private static async Task<IResult> GetStatsAsync(
        IOutboxStore outboxStore,
        IInboxStore inboxStore,
        IDeadLetterStore deadLetterStore)
    {
        var outboxStats = await outboxStore.GetStatsAsync();
        var inboxStats = await inboxStore.GetStatsAsync();
        var deadLetterCount = await deadLetterStore.GetCountAsync();

        return Results.Ok(new
        {
            outbox = outboxStats,
            inbox = inboxStats,
            deadLetterCount
        });
    }

    private static async Task<IResult> GetOutboxMessagesAsync(
        IOutboxStore store,
        int? status = null,
        int skip = 0,
        int take = 50)
    {
        var outboxStatus = status.HasValue ? (OutboxMessageStatus)status.Value : (OutboxMessageStatus?)null;
        var messages = await store.GetMessagesAsync(outboxStatus, skip, take);
        return Results.Ok(messages);
    }

    private static async Task<IResult> GetInboxMessagesAsync(
        IInboxStore store,
        int? status = null,
        int skip = 0,
        int take = 50)
    {
        var inboxStatus = status.HasValue ? (InboxMessageStatus)status.Value : (InboxMessageStatus?)null;
        var messages = await store.GetMessagesAsync(inboxStatus, skip, take);
        return Results.Ok(messages);
    }

    private static async Task<IResult> GetDeadLetterMessagesAsync(
        IDeadLetterStore store,
        int skip = 0,
        int take = 50)
    {
        var messages = await store.GetMessagesAsync(skip, take);
        return Results.Ok(messages);
    }

    private static async Task<IResult> RequeueDeadLetterAsync(
        Guid id,
        IDeadLetterStore deadLetterStore,
        IEventBusTransport transport)
    {
        var message = await deadLetterStore.GetByIdAsync(id);
        if (message == null)
            return Results.NotFound();

        await transport.PublishAsync(
            message.EventType,
            message.Payload,
            message.CorrelationId,
            message.SourceApp,
            message.TargetApp,
            message.OriginalMessageId,
            headers: null);

        await deadLetterStore.RequeueAsync(id);

        return Results.Ok(new { requeued = true });
    }

    private static IResult GetHandlersAsync(IEventHandlerRegistry registry)
    {
        var handlers = registry.GetAllHandlers()
            .Select(h => new
            {
                eventType = h.EventTypeName,
                handlerType = h.HandlerTypeName
            });

        return Results.Ok(handlers);
    }
}
