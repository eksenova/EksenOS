namespace Eksen.Core.ErrorHandling;

public class EksenException(IErrorDescriptor errorDescriptor) : Exception(errorDescriptor.Code)
{
    public IErrorDescriptor ErrorDescriptor { get; } = errorDescriptor;

    public EksenException(ErrorInstance error) : this(error.Descriptor)
    {
        foreach (var (k, v) in error.Data)
        {
            Data[k] = v;
        }
    }

    public override string Message
    {
        get { return ErrorDescriptor.Code; }
    }

    public string Code
    {
        get { return ErrorDescriptor.Code; }
    }

    public string ErrorType
    {
        get { return ErrorDescriptor.ErrorType; }
    }
}