using System.Collections.Concurrent;

namespace Eksen.EventBus;

public static class EventNameResolver
{
    private static readonly ConcurrentDictionary<Type, string> Cache = new();

    public static string GetEventName<TEvent>() where TEvent : class, IEvent
    {
        return GetEventName(typeof(TEvent));
    }

    public static string GetEventName(Type eventType)
    {
        return Cache.GetOrAdd(eventType, static type => type.FullName ?? type.Name);
    }
}
