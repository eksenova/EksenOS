using Eksen.Core.ErrorHandling;

namespace Eksen.Core;

public static class CoreErrors
{
    public static readonly string Category = AppModules.Eksen;

    public static readonly ErrorDescriptor Unauthorized = new(
        ErrorType.Authorization,
        Category);

    public delegate ErrorInstance ObjectNotFoundDelegate(Type type, object? id = null);

    public static readonly ErrorDescriptor<ObjectNotFoundDelegate> ObjectNotFound = new(Category,
        ErrorType.NotFound,
        self => (type, id) =>
            new ErrorInstance(self)
                .WithValue(id)
                .WithValue(type));

    public delegate ErrorInstance ObjectsNotFoundDelegate(Type type, ICollection<object>? ids = null);

    public static readonly ErrorDescriptor<ObjectsNotFoundDelegate> ObjectsNotFound = new(Category,
        ErrorType.NotFound,
        self => (type, ids) =>
            new ErrorInstance(self)
                .WithValue(ids)
                .WithValue(type));
}