namespace Eksen.Localization.Formatting;

public sealed record FormatParameter(string Key, object? Value);

public interface IMessageFormatter
{
    string FormatMessage(string message, params ICollection<FormatParameter> formatParameters);
}