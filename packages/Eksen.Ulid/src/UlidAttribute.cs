using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Eksen.Ulid;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class UlidAttribute() : ValidationAttribute(errorMessage: "\"{0}\" alanı geçersiz bir ULID değerine sahip.")
{
    /// <summary>Determines whether a specified object is valid.</summary>
    /// <param name="value">The object to validate.</param>
    /// <returns>
    /// <see langword="true" /> if the specified object is valid; otherwise, <see langword="false" />.</returns>
    public override bool IsValid(object? value)
    {
        if (value == null)
        {
            return true;
        }

        return value is System.Ulid || (value is string stringValue && System.Ulid.TryParse(stringValue, out _));
    }

    /// <summary>Applies formatting to a specified error message.</summary>
    /// <param name="name">The name of the field that caused the validation failure.</param>
    /// <returns>The formatted error message.</returns>
    public override string FormatErrorMessage(string name)
    {
        return string.Format(
            CultureInfo.CurrentCulture,
            ErrorMessageString, 
            name);
    }
}