using System.Reflection;

namespace Eksen.Auditing;

public sealed record EksenAuditingOptions
{
    private readonly HashSet<Type> _auditedTypes = [];

    public IReadOnlyCollection<Type> AuditedTypes
    {
        get { return _auditedTypes.ToList().AsReadOnly(); }
    }

    public bool IsEnabled { get; set; } = true;

    public bool LogHttpRequestPayload { get; set; }

    public bool LogMethodReturnValues { get; set; }

    public void AddAssembly(Assembly assembly)
    {
        List<Type> types;

        try
        {
            types = assembly.GetTypes().ToList();
        }
        catch (ReflectionTypeLoadException typeLoadException)
        {
            types = typeLoadException.Types
                .Where(t => t != null)
                .Select(x => x!)
                .ToList();
        }

        foreach (var type in types.Where(IsAuditableType))
        {
            _auditedTypes.Add(type);
        }
    }

    public void Add(Type type)
    {
        _auditedTypes.Add(type);
    }

    public void Add<T>()
    {
        _auditedTypes.Add(typeof(T));
    }

    private static bool IsAuditableType(Type type)
    {
        return (type.IsClass || type.IsInterface)
               && !type.IsAbstract
               && type.GetCustomAttribute<ExcludeFromAuditLogsAttribute>() == null;
    }
}