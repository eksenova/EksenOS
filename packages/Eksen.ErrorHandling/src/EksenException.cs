namespace Eksen.ErrorHandling;

public class EksenException(IErrorDescriptor errorDescriptor) : Exception(errorDescriptor.Code), IErrorData
{
    public EksenException(ErrorInstance error) : this(error.Descriptor)
    {
        foreach (var (k, v) in error.Data)
        {
            Data[k] = v;
        }
    }

    Dictionary<string, object?> IErrorData.Data
    {
        get
        {
            var data = new Dictionary<string, object?>();
            foreach (var key in Data.Keys)
            {
                data[(string)key] = Data[key];
            }

            return data;
        }
    }

    public IErrorDescriptor Descriptor { get; } = errorDescriptor;

    public override string Message
    {
        get { return Descriptor.Code; }
    }
}