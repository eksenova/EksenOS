using System.Diagnostics;
using Eksen.Auditing.Entities;
using Eksen.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Eksen.Auditing.AspNetCore;

public sealed class AuditingMiddleware(
    RequestDelegate next,
    IOptions<EksenAuditingOptions> options)
{
    public async Task InvokeAsync(
        HttpContext httpContext,
        IAuditLogManager auditLogManager,
        IAuthContext authContext)
    {
        if (!options.Value.IsEnabled)
        {
            await next(httpContext);
            return;
        }

        using var scope = auditLogManager.BeginScope();
        var stopwatch = Stopwatch.StartNew();

        PopulateHttpRequestInfo(httpContext, scope);

        if (options.Value.LogHttpRequestPayload)
        {
            await CaptureRequestPayloadAsync(httpContext, scope);
        }

        try
        {
            await next(httpContext);

            stopwatch.Stop();
            scope.AuditLog.SetDuration(stopwatch.Elapsed);
            scope.AuditLog.HttpRequest?.SetStatusCode(httpContext.Response.StatusCode);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            scope.AuditLog.SetDuration(stopwatch.Elapsed);
            scope.AuditLog.SetException(ex.Message);
            scope.AuditLog.HttpRequest?.SetStatusCode(httpContext.Response.StatusCode);
            throw;
        }
        finally
        {
            await auditLogManager.SaveAsync(httpContext.RequestAborted);
        }
    }

    private static void PopulateHttpRequestInfo(HttpContext httpContext, IAuditLogScope scope)
    {
        var request = httpContext.Request;
        var connection = httpContext.Connection;

        var sourceIpAddress = connection.RemoteIpAddress?.ToString();
        var sourcePort = connection.RemotePort;

        scope.AuditLog.SetSourceIpAddress(sourceIpAddress);
        scope.AuditLog.SetSourcePort(sourcePort > 0
            ? sourcePort
            : null);
        scope.AuditLog.SetCorrelationId(httpContext.TraceIdentifier);

        var httpRequest = new AuditLogHttpRequest(
            scope.AuditLog.Id,
            request.Method,
            request.Host.ToString(),
            request.Path.ToString(),
            request.QueryString.HasValue
                ? request.QueryString.ToString()
                : null,
            request.Scheme,
            request.Protocol,
            request.Headers.UserAgent.ToString(),
            request.ContentType);

        scope.AuditLog.SetHttpRequest(httpRequest);
    }

    private static async Task CaptureRequestPayloadAsync(HttpContext httpContext, IAuditLogScope scope)
    {
        var request = httpContext.Request;

        if (request.ContentLength is null or 0)
        {
            return;
        }

        if (scope.AuditLog.HttpRequest == null)
        {
            return;
        }

        request.EnableBuffering();

        using var reader = new StreamReader(request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;

        var payload = new AuditLogHttpRequestPayload(
            scope.AuditLog.HttpRequest.Id,
            body,
            request.ContentType,
            request.ContentLength);

        scope.AuditLog.HttpRequest.SetPayload(payload);
    }
}