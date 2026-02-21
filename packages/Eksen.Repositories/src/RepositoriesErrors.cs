using Eksen.Core;
using Eksen.Core.ErrorHandling;
using Eksen.ValueObjects.ErrorHandling;

namespace Eksen.Repositories;

public static class RepositoriesErrors
{
    public static readonly string Category = AppModules.Repositories;

    public static readonly ErrorDescriptor<ValueValidationError<int>> NegativeSortingIndex =
        new(Category,
            ErrorType.Validation,
            self => value =>
                new ErrorInstance(self)
                    .WithValue(value));

    public static readonly ErrorDescriptor<ValueParseError> InvalidSortingIndex =
        new(Category,
            ErrorType.Validation,
            self => value =>
                new ErrorInstance(self)
                    .WithValue(value));
}