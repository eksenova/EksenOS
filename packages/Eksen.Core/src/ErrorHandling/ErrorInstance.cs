using System.Runtime.CompilerServices;

namespace Eksen.Core.ErrorHandling;

public record ErrorInstance(IErrorDescriptor Descriptor)
{
    public Dictionary<string, object?> Data { get; private init; } = new();

    public virtual ErrorInstance WithData(Dictionary<string, object?> data)
    {
        var mergedData = Data.ToDictionary();

        foreach (var key in Data.Keys)
        {
            mergedData[key] = data[key];
        }

        return this with
        {
            Data = mergedData
        };
    }

    public virtual ErrorInstance WithData(string key, object? value)
    {
        ArgumentNullException.ThrowIfNull(key);

        var data = Data.ToDictionary();

        data[key] = value;

        return this with
        {
            Data = data
        };
    }

    public virtual ErrorInstance WithValue(
        object? paramValue,
        [CallerArgumentExpression(nameof(paramValue))]
        string? paramName = null)
    {
        ArgumentNullException.ThrowIfNull(paramName);

        return WithData(paramName, paramValue);
    }

    protected virtual EksenException ToException()
    {
        return new EksenException(this);
    }

    public static implicit operator Exception(ErrorInstance errorInstance)
    {
        return errorInstance.ToException();
    }
}