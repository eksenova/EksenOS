namespace Eksen.ErrorHandling;

public interface IErrorDescriptor
{
    string Code { get; }

    string ErrorType { get; }
}