namespace Eksen.Core.Helpers;

public static class TypeHelper
{
    public static Type GetUnderlyingType(Type type)
    {
        return GetUnderlyingType(type, out _, out _, out _);
    }

    public static Type GetUnderlyingType(Type type, out bool isNullable)
    {
        return GetUnderlyingType(type, out isNullable, out _);
    }

    public static Type GetUnderlyingType(Type type, out bool isNullable, out bool isCollection)
    {
        return GetUnderlyingType(type, out isNullable, out isCollection, out _);
    }

    public static Type GetUnderlyingType(Type type, out bool isNullable, out bool isCollection, out bool isNullableCollection)
    {
        isNullable = false;
        isCollection = false;
        isNullableCollection = false;

        var underlyingType = Nullable.GetUnderlyingType(type);
        if (underlyingType != null)
        {
            type = underlyingType;

            if (type.IsArray || type.IsCollection)
            {
                isCollection = true;
                isNullableCollection = true;
            }
            else
            {
                isNullable = true;
            }
        }

        if (type.IsArray)
        {
            isCollection = true;
            type = type.GetElementType()!;
        }

        if (type.IsCollection)
        {
            isCollection = true;
            var genericArguments = type.GetGenericArguments();
            if (genericArguments.Length == 1)
            {
                type = genericArguments[0];
            }
        }

        underlyingType = Nullable.GetUnderlyingType(type);
        if (underlyingType != null)
        {
            isNullable = true;
            type = underlyingType;
        }

        return type;
    }

    extension(Type type)
    {
        public bool IsCollection
        {
            get
            {
                if (type.IsGenericType
                    && type.GetGenericTypeDefinition() == typeof(ICollection<>))
                {
                    return true;
                }

                if (type.GetInterfaces()
                    .Any(x => x.IsGenericType
                              && x.GetGenericTypeDefinition()
                              == typeof(ICollection<>)))
                {
                    return true;
                }

                return false;
            }
        }
    }
}