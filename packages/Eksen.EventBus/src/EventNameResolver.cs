namespace Eksen.EventBus;

public static class EventNameResolver
{
    private static readonly Dictionary<Type, string> Cache = [];

    public static string GetEventName<TEvent>() where TEvent : class, IIntegrationEvent
    {
        return GetEventName(typeof(TEvent));
    }

    public static string GetEventName(Type eventType)
    {
        if (Cache.TryGetValue(eventType, out var name))
            return name;

        name = eventType.FullName ?? eventType.Name;
        Cache[eventType] = name;
        return name;
    }
}
