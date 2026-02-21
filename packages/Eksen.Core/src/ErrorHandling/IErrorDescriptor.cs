namespace Eksen.Core.ErrorHandling;

public interface IErrorDescriptor
{
    string Code { get; }

    string ErrorType { get; }
}