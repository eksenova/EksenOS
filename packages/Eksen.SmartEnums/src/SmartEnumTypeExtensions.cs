namespace Eksen.SmartEnums;

public static class SmartEnumTypeExtensions
{
    extension(Type type)
    {
        public bool IsEnumeration
        {
            get
            {
                return type is { IsClass: true, BaseType.IsGenericType: true, IsAbstract: false }
                       && typeof(Enumeration<>).IsAssignableFrom(type.BaseType.GetGenericTypeDefinition());
            }
        }
    }
}