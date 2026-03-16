namespace Eksen.EventBus;

public interface IEventHandlerRegistry
{
    void Register<TEvent, THandler>()
        where TEvent : class, IIntegrationEvent
        where THandler : class, IEventHandler<TEvent>;

    void Register(Type eventType, Type handlerType);

    IReadOnlyList<EventHandlerDescriptor> GetHandlers(string eventTypeName);

    IReadOnlyList<EventHandlerDescriptor> GetHandlers<TEvent>()
        where TEvent : class, IIntegrationEvent;

    IReadOnlyCollection<string> GetAllEventTypes();

    IReadOnlyCollection<EventHandlerDescriptor> GetAllHandlers();
}

public class EventHandlerDescriptor
{
    public required Type EventType { get; init; }

    public required Type HandlerType { get; init; }

    public string EventTypeName => EventNameResolver.GetEventName(EventType);

    public string HandlerTypeName => HandlerType.FullName ?? HandlerType.Name;
}

public class EventHandlerRegistry : IEventHandlerRegistry
{
    private readonly Dictionary<string, List<EventHandlerDescriptor>> _handlers = [];

    public void Register<TEvent, THandler>()
        where TEvent : class, IIntegrationEvent
        where THandler : class, IEventHandler<TEvent>
    {
        Register(typeof(TEvent), typeof(THandler));
    }

    public void Register(Type eventType, Type handlerType)
    {
        var eventName = EventNameResolver.GetEventName(eventType);

        if (!_handlers.TryGetValue(eventName, out var descriptors))
        {
            descriptors = [];
            _handlers[eventName] = descriptors;
        }

        var descriptor = new EventHandlerDescriptor
        {
            EventType = eventType,
            HandlerType = handlerType
        };

        if (descriptors.All(d => d.HandlerType != handlerType))
        {
            descriptors.Add(descriptor);
        }
    }

    public IReadOnlyList<EventHandlerDescriptor> GetHandlers(string eventTypeName)
    {
        return _handlers.TryGetValue(eventTypeName, out var descriptors)
            ? descriptors.AsReadOnly()
            : [];
    }

    public IReadOnlyList<EventHandlerDescriptor> GetHandlers<TEvent>()
        where TEvent : class, IIntegrationEvent
    {
        return GetHandlers(EventNameResolver.GetEventName<TEvent>());
    }

    public IReadOnlyCollection<string> GetAllEventTypes()
    {
        return _handlers.Keys;
    }

    public IReadOnlyCollection<EventHandlerDescriptor> GetAllHandlers()
    {
        return _handlers.Values.SelectMany(x => x).ToList().AsReadOnly();
    }
}
