using Eksen.ErrorHandling;

namespace Eksen.ValueObjects.ErrorHandling;

public delegate ErrorInstance ValueValidationError<in TValueType>(TValueType value);