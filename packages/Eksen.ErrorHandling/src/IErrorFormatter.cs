namespace Eksen.ErrorHandling;

public interface IErrorFormatter
{
    string FormatError(IErrorData errorData);
}