using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Net;
using System.Text.Json.Serialization;
using Eksen.Telemetry;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eksen.ErrorHandling.AspNetCore;

public class EksenExceptionHandler(
    ILogger<EksenExceptionHandler> logger,
    IErrorFormatter? errorFormatter,
    IMeterFactory? meterFactory,
    IOptionsMonitor<EksenTelemetryOptions>? telemetryOptions
) : IExceptionHandler
{
    private readonly ErrorHandlingTelemetry _telemetry = ErrorHandlingTelemetry.For(meterFactory);

    public EksenExceptionHandler(
        ILogger<EksenExceptionHandler> logger,
        IErrorFormatter? errorFormatter,
        IMeterFactory? meterFactory)
        : this(logger, errorFormatter, meterFactory, telemetryOptions: null)
    {
    }

    public EksenExceptionHandler(
        ILogger<EksenExceptionHandler> logger,
        IErrorFormatter? errorFormatter = null)
        : this(logger, errorFormatter, meterFactory: null, telemetryOptions: null)
    {
    }

    public virtual async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken = default)
    {
        var traceIdentifier = httpContext.TraceIdentifier;

        var formatter = errorFormatter ?? httpContext.RequestServices.GetRequiredService<IErrorFormatter>();
        ErrorResponseBody? response;
        HttpStatusCode? statusCode;
        var handled = errorFormatter != null
            ? TryGetErrorResponse(exception, traceIdentifier, out response, out statusCode)
            : TryGetErrorResponse(exception, traceIdentifier, formatter, out response, out statusCode);
        if (!handled)
        {
            return false;
        }

        httpContext.Response.StatusCode = (int)statusCode!.Value;
        RecordHandledError(exception, (int)statusCode.Value);
        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
        return true;
    }

    /// <summary>
    /// Tags the current (request) span with <c>error.type</c> and <c>eksen.error.kind</c> and counts the error.
    /// For 5xx responses it also marks the span failed and, unless <c>Telemetry:RecordExceptions</c> is off, adds
    /// an exception event whose message and stack trace are redacted. Client errors (4xx) are expected outcomes,
    /// so they leave the span status unset and add no event. <c>error.type</c> is the error descriptor code, or
    /// the exception type when no <see cref="EksenException"/> was found.
    /// </summary>
    protected virtual void RecordHandledError(Exception exception, int statusCode)
    {
        var descriptor = FindErrorDescriptor(exception);
        var errorType = descriptor?.Code ?? exception.GetType().FullName ?? exception.GetType().Name;
        var errorKind = descriptor?.ErrorType ?? "unhandled";

        var activity = Activity.Current;
        if (activity is not null)
        {
            activity.SetTag(EksenSemanticConventions.ErrorType, errorType);
            activity.SetTag(ErrorHandlingTelemetry.ErrorKindTag, errorKind);
            if (statusCode >= 500)
            {
                if (telemetryOptions?.CurrentValue.RecordExceptions ?? true)
                {
                    activity.RecordException(exception, markFailed: false);
                }

                activity.SetStatus(ActivityStatusCode.Error, errorType);
            }
        }

        _telemetry.ErrorsHandled.Add(
            1,
            new KeyValuePair<string, object?>(EksenSemanticConventions.ErrorType, errorType),
            new KeyValuePair<string, object?>(ErrorHandlingTelemetry.ErrorKindTag, errorKind),
            new KeyValuePair<string, object?>(EksenSemanticConventions.HttpResponseStatusCode, statusCode));
    }

    private static IErrorDescriptor? FindErrorDescriptor(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is EksenException eksenException)
            {
                return ((IErrorData)eksenException).Descriptor;
            }
        }

        return null;
    }

    protected virtual bool TryGetErrorResponse(
        Exception exception,
        string traceIdentifier,
        [NotNullWhen(returnValue: true)] out ErrorResponseBody? response,
        [NotNullWhen(returnValue: true)] out HttpStatusCode? statusCode)
    {
        return TryGetErrorResponse(exception, traceIdentifier,
            errorFormatter ?? throw new InvalidOperationException("An error formatter is required outside a request."),
            out response, out statusCode);
    }

    protected virtual bool TryGetErrorResponse(
        Exception exception,
        string traceIdentifier,
        IErrorFormatter formatter,
        [NotNullWhen(returnValue: true)] out ErrorResponseBody? response,
        [NotNullWhen(returnValue: true)] out HttpStatusCode? statusCode)
    {
        response = null;
        statusCode = null;

        switch (exception)
        {
            case EksenException eksenException:
            {
                var error = (IErrorData)eksenException;

                var printableErrorData = error.Data.Count > 0
                    ? string.Join(separator: "\n  ", error.Data.Select(kvp => $"{kvp.Key} => {kvp.Value}"))
                    : "  <No Data>";

                var errorMessage = formatter.FormatError(error);
                var resolvedStatusCode = ResolveStatusCode(error.Descriptor);

                // Client errors are expected outcomes; only server errors are logged as errors.
                logger.Log(
                    (int)resolvedStatusCode >= 500 ? LogLevel.Error : LogLevel.Warning,
                    message: "Request {TraceId} resulted in error {ErrorCode} ({StatusCode}): {ErrorMessage}\n\nError Data:\n{ErrorData}",
                    traceIdentifier, error.Descriptor.Code, (int)resolvedStatusCode, errorMessage, printableErrorData);

                response = new ErrorResponseBody
                {
                    ErrorMessage = errorMessage,
                    ErrorData = error.Data.Count > 0
                        ? error.Data
                        : null
                };

                statusCode = resolvedStatusCode;
                break;
            }

            case { InnerException: not null }:
            {
                // attempt unwrap
                return TryGetErrorResponse(exception.InnerException, traceIdentifier, formatter, out response, out statusCode);
            }
        }

        return response != null;
    }

    protected virtual HttpStatusCode ResolveStatusCode(IErrorDescriptor descriptor)
    {
        if (descriptor.ErrorType == ErrorType.NotFound)
        {
            return HttpStatusCode.NotFound;
        }

        if (descriptor.ErrorType == ErrorType.Authorization)
        {
            return HttpStatusCode.Unauthorized;
        }

        if (descriptor.ErrorType == ErrorType.Validation)
        {
            return HttpStatusCode.BadRequest;
        }

        if (descriptor.ErrorType == ErrorType.RateLimit)
        {
            return HttpStatusCode.TooManyRequests;
        }

        if (descriptor.ErrorType == ErrorType.Conflict)
        {
            return HttpStatusCode.Conflict;
        }

        return HttpStatusCode.InternalServerError;
    }
}

public record ErrorResponseBody
{
    [UsedImplicitly]
    public string? ErrorMessage { get; set; }

    [UsedImplicitly]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object?>? ErrorData { get; set; }
}