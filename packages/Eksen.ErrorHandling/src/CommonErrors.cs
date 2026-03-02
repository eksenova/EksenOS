using Eksen.Core;

namespace Eksen.ErrorHandling;

public static class CommonErrors
{
    public static readonly string Category = AppModules.Eksen;

    public static readonly ErrorDescriptor Unauthorized = new(
        ErrorType.Authorization,
        Category);

    public delegate ErrorInstance ObjectNotFoundDelegate(Type type, object? id = null);

    public static readonly ErrorDescriptor<ObjectNotFoundDelegate> ObjectNotFound = new(
        ErrorType.NotFound,
        Category,
        self => (type, id) =>
            new ErrorInstance(self)
                .WithValue(id)
                .WithValue(type));

    public delegate ErrorInstance ObjectsNotFoundDelegate(Type type, ICollection<object>? ids = null);

    public static readonly ErrorDescriptor<ObjectsNotFoundDelegate> ObjectsNotFound = new(
        ErrorType.NotFound,
        Category,
        self => (type, ids) =>
            new ErrorInstance(self)
                .WithValue(ids)
                .WithValue(type));
}