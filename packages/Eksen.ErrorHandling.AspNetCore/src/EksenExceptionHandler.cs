using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json.Serialization;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Eksen.ErrorHandling.AspNetCore;

public class EksenExceptionHandler(
    ILogger<EksenExceptionHandler> logger,
    IErrorFormatter errorFormatter
) : IExceptionHandler
{
    public virtual async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken = default)
    {
        var traceIdentifier = httpContext.TraceIdentifier;

        if (!TryGetErrorResponse(exception, traceIdentifier, out var response, out var statusCode))
        {
            return false;
        }

        httpContext.Response.StatusCode = (int)statusCode;
        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
        return true;
    }

    protected virtual bool TryGetErrorResponse(
        Exception exception,
        string traceIdentifier,
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

                var errorMessage = errorFormatter.FormatError(error);

                logger.LogError(message: "Request {TraceId} resulted in a validation error: {ErrorMessage}\n\nError Data:\n{ErrorData}",
                    traceIdentifier, errorMessage, printableErrorData);

                response = new ErrorResponseBody
                {
                    ErrorMessage = errorMessage,
                    ErrorData = error.Data.Count > 0
                        ? error.Data
                        : null
                };

                statusCode = ResolveStatusCode(error.Descriptor);
                break;
            }

            case { InnerException: not null }:
            {
                // attempt unwrap
                return TryGetErrorResponse(exception.InnerException, traceIdentifier, out response, out statusCode);
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