using System.Diagnostics;
using Eksen.EventBus.Outbox;
using Eksen.EventBus.Telemetry;
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
        where TEvent : class, IEvent
    {
        return PublishAsync(@event, options: null, cancellationToken);
    }

    public async Task PublishAsync<TEvent>(
        TEvent @event,
        PublishOptions? options,
        CancellationToken cancellationToken = default)
        where TEvent : class, IEvent
    {
        var dispatchMode = options?.DispatchMode ?? EventDispatchMode.Immediate;
        var currentScope = unitOfWorkManager.Current;

        if (@event is ILocalEvent localEvent)
        {
            if (dispatchMode == EventDispatchMode.AfterUnitOfWork && currentScope != null)
            {
                currentScope.AddPostCommitAction((sp, ct) =>
                    sp.GetRequiredService<ILocalEventDispatcher>()
                        .DispatchAsync(localEvent, typeof(TEvent), ct));

                return;
            }

            await innerEventBus.PublishAsync(@event, options, cancellationToken);
            return;
        }

        if (dispatchMode == EventDispatchMode.AfterUnitOfWork && currentScope != null)
        {
            var opts = eventBusOptions.Value;
            var eventTypeName = EventNameResolver.GetEventName<TEvent>();

            // Capture the trace context at publish time; the flush runs in the unit of work's completing callback,
            // where the caller's span may already have ended.
            using var activity = DefaultEventBus.StartCreateActivity(eventTypeName, @event.EventId);
            var pending = new PendingEvent
            {
                EventTypeName = eventTypeName,
                Payload = serializer.Serialize(@event),
                CorrelationId = options?.CorrelationId ?? @event.CorrelationId,
                SourceApp = @event.SourceApp ?? opts.AppName,
                TargetApp = options?.TargetApp,
                EventId = @event.EventId,
                Headers = EventBusActivitySource.WithTraceContext(activity ?? Activity.Current, options?.Headers)
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

        scope.AddCompletedCallback((_, _) =>
        {
            _scopeContexts.Remove(scope.ScopeId);
            return Task.CompletedTask;
        });
        scope.AddRollbackCallback((_, _) =>
        {
            _scopeContexts.Remove(scope.ScopeId);
            return Task.CompletedTask;
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
                EventId = pending.EventId,
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
        }
    }

    private sealed class ScopeEventContext
    {
        public List<PendingEvent> PendingEvents { get; } = [];
    }
}
