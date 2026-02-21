namespace Eksen.Core.ErrorHandling;

public record BaseErrorDescriptor : IErrorDescriptor
{
    public string Code { get; }

    public string ErrorType { get; }

    internal BaseErrorDescriptor(string errorType, string category, string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorType);

        Code = $"{category}.{code}";
        ErrorType = errorType;
    }
}