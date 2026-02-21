namespace Eksen.SmartEnums;

public static class EnumerationExtensions
{
    extension(Type type)
    {
        public bool IsEnumeration
        {
            get
            {
                return type is { IsClass: true, BaseType.IsGenericType: true }
                       && typeof(Enumeration<>).IsAssignableFrom(type.BaseType.GetGenericTypeDefinition());
            }
        }
    }
}