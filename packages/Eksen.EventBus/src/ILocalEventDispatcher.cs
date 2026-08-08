using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Eksen.EventBus;

public interface ILocalEventDispatcher
{
    Task DispatchAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, ILocalEvent;

    Task DispatchAsync(ILocalEvent @event, Type eventType, CancellationToken cancellationToken = default);
}

public sealed class LocalEventDispatcher(IServiceProvider serviceProvider) : ILocalEventDispatcher
{
    private static readonly ConcurrentDictionary<Type, HandlerInvoker> Invokers = new();

    public Task DispatchAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, ILocalEvent
    {
        return InvokeAllAsync(
            serviceProvider.GetServices<ILocalEventHandler<TEvent>>(),
            handler => handler.HandleAsync(@event, cancellationToken));
    }

    public Task DispatchAsync(ILocalEvent @event, Type eventType, CancellationToken cancellationToken = default)
    {
        var invoker = Invokers.GetOrAdd(eventType, static type => new HandlerInvoker(type));

        return invoker.InvokeAsync(serviceProvider, @event, cancellationToken);
    }

    private static async Task InvokeAllAsync<THandler>(
        IEnumerable<THandler> handlers,
        Func<THandler, Task> invoke)
    {
        foreach (var handler in handlers)
        {
            await invoke(handler);
        }
    }

    private sealed class HandlerInvoker
    {
        private readonly Type _handlerServiceType;

        private readonly MethodInfo _handleMethod;

        public HandlerInvoker(Type eventType)
        {
            _handlerServiceType = typeof(ILocalEventHandler<>).MakeGenericType(eventType);

            var handleDeclaringType = typeof(IEventHandler<>).MakeGenericType(eventType);

            _handleMethod = handleDeclaringType.GetMethod(nameof(IEventHandler<ILocalEvent>.HandleAsync))
                ?? throw new InvalidOperationException(
                    $"{handleDeclaringType.Name} does not declare {nameof(IEventHandler<ILocalEvent>.HandleAsync)}.");
        }

        public async Task InvokeAsync(
            IServiceProvider serviceProvider,
            ILocalEvent @event,
            CancellationToken cancellationToken)
        {
            var handlers = serviceProvider.GetServices(_handlerServiceType);

            foreach (var handler in handlers)
            {
                if (handler == null)
                {
                    continue;
                }

                await (Task)_handleMethod.Invoke(handler, [@event, cancellationToken])!;
            }
        }
    }
}
