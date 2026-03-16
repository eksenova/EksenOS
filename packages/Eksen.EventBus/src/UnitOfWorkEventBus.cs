using Eksen.EventBus.Outbox;
using Eksen.UnitOfWork;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Eksen.EventBus;

internal sealed class UnitOfWorkEventBus(
    DefaultEventBus innerEventBus,
    IUnitOfWorkManager unitOfWorkManager,
    IEventSerializer serializer,
    IOptions<EksenEventBusOptions> eventBusOptions) : IEventBus
{
    private readonly Dictionary<Guid, ScopeEventContext> _scopeContexts = [];

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent
    {
        return PublishAsync(@event, options: null, cancellationToken);
    }

    public async Task PublishAsync<TEvent>(
        TEvent @event,
        PublishOptions? options,
        CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent
    {
        var dispatchMode = options?.DispatchMode ?? EventDispatchMode.Immediate;
        var currentScope = unitOfWorkManager.Current;

        if (dispatchMode == EventDispatchMode.AfterUnitOfWork && currentScope != null)
        {
            var opts = eventBusOptions.Value;
            var pending = new PendingEvent
            {
                EventTypeName = EventNameResolver.GetEventName<TEvent>(),
                Payload = serializer.Serialize(@event),
                CorrelationId = options?.CorrelationId ?? @event.CorrelationId,
                SourceApp = @event.SourceApp ?? opts.AppName,
                TargetApp = options?.TargetApp,
                EventId = @event.EventId,
                Headers = options?.Headers
            };

            var context = GetOrCreateScopeContext(currentScope);
            context.PendingEvents.Add(pending);
            return;
        }

        await innerEventBus.PublishAsync(@event, options, cancellationToken);
    }

    private ScopeEventContext GetOrCreateScopeContext(IUnitOfWorkScope scope)
    {
        if (_scopeContexts.TryGetValue(scope.ScopeId, out var context))
        {
            return context;
        }

        context = new ScopeEventContext();
        _scopeContexts[scope.ScopeId] = context;

        scope.AddCompletingCallback(async (sp, ct) =>
        {
            await FlushEventsToOutboxAsync(context, sp, ct);
        });

        scope.AddPostCommitAction(async (sp, ct) =>
        {
            await WaitForEventProcessingAsync(context, sp, ct);
        });

        return context;
    }

    private static async Task FlushEventsToOutboxAsync(
        ScopeEventContext context,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        if (context.PendingEvents.Count == 0)
        {
            return;
        }

        var outboxStore = serviceProvider.GetRequiredService<IOutboxStore>();

        foreach (var pending in context.PendingEvents)
        {
            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                EventType = pending.EventTypeName,
                Payload = pending.Payload,
                CreationTime = DateTime.UtcNow,
                Status = OutboxMessageStatus.Pending,
                CorrelationId = pending.CorrelationId,
                SourceApp = pending.SourceApp,
                TargetApp = pending.TargetApp,
                Headers = pending.Headers
            };

            await outboxStore.SaveAsync(outboxMessage, cancellationToken);
            context.OutboxMessageIds.Add(outboxMessage.Id);
        }
    }

    private static async Task WaitForEventProcessingAsync(
        ScopeEventContext context,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        if (context.OutboxMessageIds.Count == 0)
        {
            return;
        }

        var outboxStore = serviceProvider.GetRequiredService<IOutboxStore>();
        var pollingInterval = TimeSpan.FromSeconds(1);

        while (!cancellationToken.IsCancellationRequested)
        {
            var allResolved = true;

            foreach (var messageId in context.OutboxMessageIds)
            {
                var message = await outboxStore.GetByIdAsync(messageId, cancellationToken);

                if (message is { Status: OutboxMessageStatus.Pending or OutboxMessageStatus.Processing })
                {
                    allResolved = false;
                    break;
                }
            }

            if (allResolved)
            {
                return;
            }

            await Task.Delay(pollingInterval, cancellationToken);
        }
    }

    private sealed class ScopeEventContext
    {
        public List<PendingEvent> PendingEvents { get; } = [];

        public List<Guid> OutboxMessageIds { get; } = [];
    }
}
