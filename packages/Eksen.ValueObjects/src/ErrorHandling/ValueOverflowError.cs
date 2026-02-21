using Eksen.Core.ErrorHandling;

namespace Eksen.ValueObjects.ErrorHandling;

public delegate ErrorInstance ValueOverflowError<in TValueType>(TValueType value, TValueType maxValue);

public delegate ErrorInstance ValueLengthOverflowError(string value, int maxLength);