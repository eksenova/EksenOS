using System.Text;
using Eksen.Auditing.Entities;
using Eksen.Identity;
using Eksen.TestBase;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace Eksen.Auditing.AspNetCore.Tests;

public class AuditingMiddlewareTests : EksenUnitTestBase
{
    private readonly Mock<IAuditLogManager> _auditLogManager = new();
    private readonly Mock<IAuthContext> _authContext = new();
    private readonly EksenAuditingOptions _options = new();

    private AuditingMiddleware CreateMiddleware(RequestDelegate next)
    {
        return new AuditingMiddleware(next, Options.Create(_options));
    }

    #region Disabled

    [Fact]
    public async Task InvokeAsync_Should_Skip_Auditing_When_Disabled()
    {
        // Arrange
        _options.IsEnabled = false;
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = CreateMiddleware(next);
        var httpContext = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(httpContext, _auditLogManager.Object, _authContext.Object);

        // Assert
        nextCalled.ShouldBeTrue();
        _auditLogManager.Verify(m => m.BeginScope(), Times.Never);
        _auditLogManager.Verify(m => m.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Normal Flow

    [Fact]
    public async Task InvokeAsync_Should_Create_Scope_And_Save_On_Success()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        var scope = new AuditLogScope(auditLog);

        _auditLogManager.Setup(m => m.BeginScope()).Returns(scope);

        RequestDelegate next = _ => Task.CompletedTask;

        var middleware = CreateMiddleware(next);
        var httpContext = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(httpContext, _auditLogManager.Object, _authContext.Object);

        // Assert
        _auditLogManager.Verify(m => m.BeginScope(), Times.Once);
        _auditLogManager.Verify(m => m.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
        auditLog.Duration.ShouldNotBeNull();
        auditLog.HttpRequest.ShouldNotBeNull();
    }

    [Fact]
    public async Task InvokeAsync_Should_Set_Duration_On_Success()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        var scope = new AuditLogScope(auditLog);

        _auditLogManager.Setup(m => m.BeginScope()).Returns(scope);

        RequestDelegate next = async _ => await Task.Delay(50);

        var middleware = CreateMiddleware(next);
        var httpContext = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(httpContext, _auditLogManager.Object, _authContext.Object);

        // Assert
        auditLog.Duration.ShouldNotBeNull();
        auditLog.Duration.Value.TotalMilliseconds.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task InvokeAsync_Should_Set_HttpRequest_StatusCode_On_Success()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        var scope = new AuditLogScope(auditLog);

        _auditLogManager.Setup(m => m.BeginScope()).Returns(scope);

        RequestDelegate next = ctx =>
        {
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        };

        var middleware = CreateMiddleware(next);
        var httpContext = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(httpContext, _auditLogManager.Object, _authContext.Object);

        // Assert
        auditLog.HttpRequest.ShouldNotBeNull();
        auditLog.HttpRequest.StatusCode.ShouldBe(200);
    }

    #endregion

    #region HttpRequest Population

    [Fact]
    public async Task InvokeAsync_Should_Populate_HttpRequest_Info()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        var scope = new AuditLogScope(auditLog);

        _auditLogManager.Setup(m => m.BeginScope()).Returns(scope);

        RequestDelegate next = _ => Task.CompletedTask;

        var middleware = CreateMiddleware(next);
        var httpContext = CreateHttpContext(
            method: "POST",
            host: "example.com",
            path: "/api/test",
            queryString: "?key=value",
            scheme: "https",
            protocol: "HTTP/2");

        // Act
        await middleware.InvokeAsync(httpContext, _auditLogManager.Object, _authContext.Object);

        // Assert
        auditLog.HttpRequest.ShouldNotBeNull();
        auditLog.HttpRequest.Method.ShouldBe("POST");
        auditLog.HttpRequest.Host.ShouldBe("example.com");
        auditLog.HttpRequest.Path.ShouldBe("/api/test");
        auditLog.HttpRequest.QueryString.ShouldBe("?key=value");
        auditLog.HttpRequest.Scheme.ShouldBe("https");
        auditLog.HttpRequest.Protocol.ShouldBe("HTTP/2");
    }

    [Fact]
    public async Task InvokeAsync_Should_Set_CorrelationId_From_TraceIdentifier()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        var scope = new AuditLogScope(auditLog);

        _auditLogManager.Setup(m => m.BeginScope()).Returns(scope);

        RequestDelegate next = _ => Task.CompletedTask;

        var middleware = CreateMiddleware(next);
        var httpContext = CreateHttpContext();
        httpContext.TraceIdentifier = "trace-abc-123";

        // Act
        await middleware.InvokeAsync(httpContext, _auditLogManager.Object, _authContext.Object);

        // Assert
        auditLog.CorrelationId.ShouldBe("trace-abc-123");
    }

    [Fact]
    public async Task InvokeAsync_Should_Not_Set_QueryString_When_Absent()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        var scope = new AuditLogScope(auditLog);

        _auditLogManager.Setup(m => m.BeginScope()).Returns(scope);

        RequestDelegate next = _ => Task.CompletedTask;

        var middleware = CreateMiddleware(next);
        var httpContext = CreateHttpContext(queryString: null);

        // Act
        await middleware.InvokeAsync(httpContext, _auditLogManager.Object, _authContext.Object);

        // Assert
        auditLog.HttpRequest.ShouldNotBeNull();
        auditLog.HttpRequest.QueryString.ShouldBeNull();
    }

    #endregion

    #region Exception Flow

    [Fact]
    public async Task InvokeAsync_Should_Set_Exception_And_Rethrow_On_Failure()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        var scope = new AuditLogScope(auditLog);

        _auditLogManager.Setup(m => m.BeginScope()).Returns(scope);

        var expectedException = new InvalidOperationException("Something went wrong");
        RequestDelegate next = _ => throw expectedException;

        var middleware = CreateMiddleware(next);
        var httpContext = CreateHttpContext();

        // Act & Assert
        var thrown = await Should.ThrowAsync<InvalidOperationException>(
            () => middleware.InvokeAsync(httpContext, _auditLogManager.Object, _authContext.Object));

        thrown.ShouldBe(expectedException);
        auditLog.ExceptionMessage.ShouldBe("Something went wrong");
        auditLog.Duration.ShouldNotBeNull();
    }

    [Fact]
    public async Task InvokeAsync_Should_Save_After_Exception()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        var scope = new AuditLogScope(auditLog);

        _auditLogManager.Setup(m => m.BeginScope()).Returns(scope);

        RequestDelegate next = _ => throw new Exception("error");

        var middleware = CreateMiddleware(next);
        var httpContext = CreateHttpContext();

        // Act & Assert
        await Should.ThrowAsync<Exception>(
            () => middleware.InvokeAsync(httpContext, _auditLogManager.Object, _authContext.Object));

        _auditLogManager.Verify(m => m.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Payload Capture

    [Fact]
    public async Task InvokeAsync_Should_Capture_RequestPayload_When_Enabled()
    {
        // Arrange
        _options.LogHttpRequestPayload = true;

        var auditLog = new AuditLog(null, null, null, null, null);
        var scope = new AuditLogScope(auditLog);

        _auditLogManager.Setup(m => m.BeginScope()).Returns(scope);

        RequestDelegate next = _ => Task.CompletedTask;

        var middleware = CreateMiddleware(next);
        var bodyContent = "{\"name\":\"test\"}";
        var httpContext = CreateHttpContext(body: bodyContent, contentType: "application/json");

        // Act
        await middleware.InvokeAsync(httpContext, _auditLogManager.Object, _authContext.Object);

        // Assert
        auditLog.HttpRequest.ShouldNotBeNull();
        auditLog.HttpRequest.Payload.ShouldNotBeNull();
        auditLog.HttpRequest.Payload.RequestBody.ShouldBe(bodyContent);
        auditLog.HttpRequest.Payload.ContentType.ShouldBe("application/json");
    }

    [Fact]
    public async Task InvokeAsync_Should_Not_Capture_Payload_When_Disabled()
    {
        // Arrange
        _options.LogHttpRequestPayload = false;

        var auditLog = new AuditLog(null, null, null, null, null);
        var scope = new AuditLogScope(auditLog);

        _auditLogManager.Setup(m => m.BeginScope()).Returns(scope);

        RequestDelegate next = _ => Task.CompletedTask;

        var middleware = CreateMiddleware(next);
        var httpContext = CreateHttpContext(body: "{\"name\":\"test\"}", contentType: "application/json");

        // Act
        await middleware.InvokeAsync(httpContext, _auditLogManager.Object, _authContext.Object);

        // Assert
        auditLog.HttpRequest.ShouldNotBeNull();
        auditLog.HttpRequest.Payload.ShouldBeNull();
    }

    [Fact]
    public async Task InvokeAsync_Should_Not_Capture_Payload_When_ContentLength_Is_Null()
    {
        // Arrange
        _options.LogHttpRequestPayload = true;

        var auditLog = new AuditLog(null, null, null, null, null);
        var scope = new AuditLogScope(auditLog);

        _auditLogManager.Setup(m => m.BeginScope()).Returns(scope);

        RequestDelegate next = _ => Task.CompletedTask;

        var middleware = CreateMiddleware(next);
        var httpContext = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(httpContext, _auditLogManager.Object, _authContext.Object);

        // Assert
        auditLog.HttpRequest.ShouldNotBeNull();
        auditLog.HttpRequest.Payload.ShouldBeNull();
    }

    [Fact]
    public async Task InvokeAsync_Should_Not_Capture_Payload_When_ContentLength_Is_Zero()
    {
        // Arrange
        _options.LogHttpRequestPayload = true;

        var auditLog = new AuditLog(null, null, null, null, null);
        var scope = new AuditLogScope(auditLog);

        _auditLogManager.Setup(m => m.BeginScope()).Returns(scope);

        RequestDelegate next = _ => Task.CompletedTask;

        var middleware = CreateMiddleware(next);
        var httpContext = CreateHttpContext();
        httpContext.Request.ContentLength = 0;

        // Act
        await middleware.InvokeAsync(httpContext, _auditLogManager.Object, _authContext.Object);

        // Assert
        auditLog.HttpRequest.ShouldNotBeNull();
        auditLog.HttpRequest.Payload.ShouldBeNull();
    }

    [Fact]
    public async Task InvokeAsync_Should_Reset_Request_Body_Position_After_Capture()
    {
        // Arrange
        _options.LogHttpRequestPayload = true;

        var auditLog = new AuditLog(null, null, null, null, null);
        var scope = new AuditLogScope(auditLog);

        _auditLogManager.Setup(m => m.BeginScope()).Returns(scope);

        RequestDelegate next = async ctx =>
        {
            using var reader = new StreamReader(ctx.Request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            body.ShouldBe("{\"name\":\"test\"}");
        };

        var middleware = CreateMiddleware(next);
        var httpContext = CreateHttpContext(body: "{\"name\":\"test\"}", contentType: "application/json");

        // Act
        await middleware.InvokeAsync(httpContext, _auditLogManager.Object, _authContext.Object);

        // Assert
        auditLog.HttpRequest.ShouldNotBeNull();
        auditLog.HttpRequest.Payload.ShouldNotBeNull();
    }

    #endregion

    #region Helpers

    private static DefaultHttpContext CreateHttpContext(
        string method = "GET",
        string host = "localhost",
        string path = "/",
        string? queryString = null,
        string scheme = "https",
        string protocol = "HTTP/1.1",
        string? body = null,
        string? contentType = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = method;
        httpContext.Request.Host = new HostString(host);
        httpContext.Request.Path = path;
        httpContext.Request.Scheme = scheme;
        httpContext.Request.Protocol = protocol;

        if (queryString != null)
        {
            httpContext.Request.QueryString = new QueryString(queryString);
        }

        if (body != null)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            httpContext.Request.Body = new MemoryStream(bytes);
            httpContext.Request.ContentLength = bytes.Length;
            httpContext.Request.ContentType = contentType;
        }

        return httpContext;
    }

    #endregion
}
