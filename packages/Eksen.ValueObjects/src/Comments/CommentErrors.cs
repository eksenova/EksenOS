using Eksen.Core;
using Eksen.ErrorHandling;
using Eksen.ValueObjects.ErrorHandling;

namespace Eksen.ValueObjects.Comments;

public static class CommentErrors
{
    public static readonly string Category = $"{AppModules.ValueObjects}.Comments";

    public static readonly ErrorDescriptor<ValueLengthOverflowError> ActionCommentTooLong = new(
        ErrorType.Validation,
        Category,
        self => (value, maxLength) =>
            new ErrorInstance(self)
                .WithValue(value)
                .WithValue(maxLength));
}
