using Eksen.Auditing.Entities;
using Eksen.Identity.Tenants;
using Eksen.Identity.Users;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Auditing.Tests.Entities;

public class AuditLogTests : EksenUnitTestBase
{
    [Fact]
    public void Constructor_Should_Create_AuditLog_With_All_Parameters()
    {
        // Arrange
        var userId = EksenUserId.NewId();
        var tenantId = EksenTenantId.NewId();
        var sourceIp = "192.168.1.1";
        var sourcePort = 8080;
        var correlationId = "corr-123";

        // Act
        var auditLog = new AuditLog(userId, tenantId, sourceIp, sourcePort, correlationId);

        // Assert
        auditLog.Id.ShouldNotBeNull();
        auditLog.UserId.ShouldBe(userId);
        auditLog.TenantId.ShouldBe(tenantId);
        auditLog.SourceIpAddress.ShouldBe(sourceIp);
        auditLog.SourcePort.ShouldBe(sourcePort);
        auditLog.CorrelationId.ShouldBe(correlationId);
        auditLog.LogTime.ShouldBeInRange(DateTime.UtcNow.AddSeconds(-5), DateTime.UtcNow.AddSeconds(1));
        auditLog.Actions.ShouldBeEmpty();
        auditLog.EntityChanges.ShouldBeEmpty();
        auditLog.Duration.ShouldBeNull();
        auditLog.ExceptionMessage.ShouldBeNull();
        auditLog.Metadata.ShouldBeNull();
        auditLog.HttpRequest.ShouldBeNull();
    }

    [Fact]
    public void Constructor_Should_Create_AuditLog_With_Null_Optional_Parameters()
    {
        // Arrange & Act
        var auditLog = new AuditLog(null, null, null, null, null);

        // Assert
        auditLog.Id.ShouldNotBeNull();
        auditLog.UserId.ShouldBeNull();
        auditLog.TenantId.ShouldBeNull();
        auditLog.SourceIpAddress.ShouldBeNull();
        auditLog.SourcePort.ShouldBeNull();
        auditLog.CorrelationId.ShouldBeNull();
    }

    [Fact]
    public void SetDuration_Should_Set_Duration()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        var duration = TimeSpan.FromMilliseconds(150);

        // Act
        auditLog.SetDuration(duration);

        // Assert
        auditLog.Duration.ShouldBe(duration);
    }

    [Fact]
    public void SetException_Should_Set_ExceptionMessage()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        var message = "Something went wrong";

        // Act
        auditLog.SetException(message);

        // Assert
        auditLog.ExceptionMessage.ShouldBe(message);
    }

    [Fact]
    public void SetMetadata_Should_Set_Metadata()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        var metadata = """{"key":"value"}""";

        // Act
        auditLog.SetMetadata(metadata);

        // Assert
        auditLog.Metadata.ShouldBe(metadata);
    }

    [Fact]
    public void SetSourceIpAddress_Should_Set_SourceIpAddress()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);

        // Act
        auditLog.SetSourceIpAddress("10.0.0.1");

        // Assert
        auditLog.SourceIpAddress.ShouldBe("10.0.0.1");
    }

    [Fact]
    public void SetSourcePort_Should_Set_SourcePort()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);

        // Act
        auditLog.SetSourcePort(443);

        // Assert
        auditLog.SourcePort.ShouldBe(443);
    }

    [Fact]
    public void SetCorrelationId_Should_Set_CorrelationId()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);

        // Act
        auditLog.SetCorrelationId("new-corr-id");

        // Assert
        auditLog.CorrelationId.ShouldBe("new-corr-id");
    }

    [Fact]
    public void SetHttpRequest_Should_Set_HttpRequest()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        var httpRequest = new AuditLogHttpRequest(
            auditLog.Id, "GET", "localhost", "/api/test", null, "https", "HTTP/1.1", "TestAgent", "application/json");

        // Act
        auditLog.SetHttpRequest(httpRequest);

        // Assert
        auditLog.HttpRequest.ShouldBe(httpRequest);
    }

    [Fact]
    public void AddAction_Should_Add_Action_To_Collection()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        var action = new AuditLogAction(auditLog.Id, "TestService", "TestMethod", null);

        // Act
        auditLog.AddAction(action);

        // Assert
        auditLog.Actions.Count.ShouldBe(1);
        auditLog.Actions.ShouldContain(action);
    }

    [Fact]
    public void AddAction_Should_Add_Multiple_Actions()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        var action1 = new AuditLogAction(auditLog.Id, "Service1", "Method1", null);
        var action2 = new AuditLogAction(auditLog.Id, "Service2", "Method2", """{"param":"value"}""");

        // Act
        auditLog.AddAction(action1);
        auditLog.AddAction(action2);

        // Assert
        auditLog.Actions.Count.ShouldBe(2);
    }

    [Fact]
    public void AddEntityChange_Should_Add_EntityChange_To_Collection()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        var entityChange = new AuditLogEntityChange(
            auditLog.Id, EntityChangeType.Created, "MyApp.Entities.Order", "order-123");

        // Act
        auditLog.AddEntityChange(entityChange);

        // Assert
        auditLog.EntityChanges.Count.ShouldBe(1);
        auditLog.EntityChanges.ShouldContain(entityChange);
    }
}
