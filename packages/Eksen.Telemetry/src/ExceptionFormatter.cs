using System.Text;

namespace Eksen.Telemetry;

public readonly record struct FormattedException(string Type, string? Message, string? StackTrace)
{
    public IEnumerable<KeyValuePair<string, object?>> ToAttributes()
    {
        yield return new KeyValuePair<string, object?>(EksenSemanticConventions.ExceptionType, Type);

        if (!string.IsNullOrEmpty(Message))
        {
            yield return new KeyValuePair<string, object?>(EksenSemanticConventions.ExceptionMessage, Message);
        }

        if (!string.IsNullOrEmpty(StackTrace))
        {
            yield return new KeyValuePair<string, object?>(EksenSemanticConventions.ExceptionStacktrace, StackTrace);
        }
    }
}

/// <summary>
/// Splits an exception into the OpenTelemetry <c>exception.*</c> attributes. Unlike
/// <see cref="Exception.ToString"/>, the stack trace does not repeat the message; inner exceptions are
/// introduced by a <c> ---&gt; [Type]</c> line.
/// </summary>
public static class ExceptionFormatter
{
    public static FormattedException Format(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var stackTrace = BuildStackTraceWithoutMessage(exception);

        return new FormattedException(
            exception.GetType().ToString(),
            string.IsNullOrEmpty(exception.Message) ? null : exception.Message,
            stackTrace.Length == 0 ? null : stackTrace);
    }

    private static string BuildStackTraceWithoutMessage(Exception exception)
    {
        var builder = new StringBuilder();
        var current = exception;
        var depth = 0;

        while (current is not null)
        {
            if (depth > 0)
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.Append(" ---> [").Append(current.GetType()).Append(']').AppendLine();
            }

            if (!string.IsNullOrEmpty(current.StackTrace))
            {
                builder.Append(current.StackTrace);
            }

            current = current.InnerException;
            depth++;
        }

        return builder.ToString();
    }
}
