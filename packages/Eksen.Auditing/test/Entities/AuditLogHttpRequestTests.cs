using Eksen.Auditing.Entities;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Auditing.Tests.Entities;

public class AuditLogHttpRequestTests : EksenUnitTestBase
{
    [Fact]
    public void Constructor_Should_Create_HttpRequest_With_All_Parameters()
    {
        // Arrange
        var auditLogId = AuditLogId.NewId();
        var method = "POST";
        var host = "api.example.com";
        var path = "/api/orders";
        var queryString = "?page=1";
        var scheme = "https";
        var protocol = "HTTP/2";
        var userAgent = "Mozilla/5.0";
        var contentType = "application/json";

        // Act
        var httpRequest = new AuditLogHttpRequest(
            auditLogId, method, host, path, queryString, scheme, protocol, userAgent, contentType);

        // Assert
        httpRequest.Id.ShouldNotBeNull();
        httpRequest.AuditLogId.ShouldBe(auditLogId);
        httpRequest.Method.ShouldBe(method);
        httpRequest.Host.ShouldBe(host);
        httpRequest.Path.ShouldBe(path);
        httpRequest.QueryString.ShouldBe(queryString);
        httpRequest.Scheme.ShouldBe(scheme);
        httpRequest.Protocol.ShouldBe(protocol);
        httpRequest.UserAgent.ShouldBe(userAgent);
        httpRequest.ContentType.ShouldBe(contentType);
        httpRequest.StatusCode.ShouldBeNull();
        httpRequest.Payload.ShouldBeNull();
    }

    [Fact]
    public void Constructor_Should_Allow_Null_Optional_Parameters()
    {
        // Arrange & Act
        var httpRequest = new AuditLogHttpRequest(
            AuditLogId.NewId(), "GET", "localhost", "/", null, null, null, null, null);

        // Assert
        httpRequest.QueryString.ShouldBeNull();
        httpRequest.Scheme.ShouldBeNull();
        httpRequest.Protocol.ShouldBeNull();
        httpRequest.UserAgent.ShouldBeNull();
        httpRequest.ContentType.ShouldBeNull();
    }

    [Fact]
    public void SetStatusCode_Should_Set_StatusCode()
    {
        // Arrange
        var httpRequest = new AuditLogHttpRequest(
            AuditLogId.NewId(), "GET", "localhost", "/api/test", null, null, null, null, null);

        // Act
        httpRequest.SetStatusCode(200);

        // Assert
        httpRequest.StatusCode.ShouldBe(200);
    }

    [Fact]
    public void SetPayload_Should_Set_Payload()
    {
        // Arrange
        var httpRequest = new AuditLogHttpRequest(
            AuditLogId.NewId(), "POST", "localhost", "/api/test", null, null, null, null, "application/json");

        var payload = new AuditLogHttpRequestPayload(
            httpRequest.Id, """{"name":"test"}""", "application/json", 15);

        // Act
        httpRequest.SetPayload(payload);

        // Assert
        httpRequest.Payload.ShouldBe(payload);
    }
}
