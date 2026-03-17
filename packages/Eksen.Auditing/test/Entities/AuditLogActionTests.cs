using Eksen.Auditing.Entities;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Auditing.Tests.Entities;

public class AuditLogActionTests : EksenUnitTestBase
{
    [Fact]
    public void Constructor_Should_Create_Action_With_All_Parameters()
    {
        // Arrange
        var auditLogId = AuditLogId.NewId();
        var serviceType = "MyApp.Services.OrderAppService";
        var methodName = "CreateAsync";
        var parameters = """{"orderNumber":"ORD-001"}""";

        // Act
        var action = new AuditLogAction(auditLogId, serviceType, methodName, parameters);

        // Assert
        action.Id.ShouldNotBeNull();
        action.AuditLogId.ShouldBe(auditLogId);
        action.ServiceType.ShouldBe(serviceType);
        action.MethodName.ShouldBe(methodName);
        action.Parameters.ShouldBe(parameters);
        action.LogTime.ShouldBeInRange(DateTime.UtcNow.AddSeconds(-5), DateTime.UtcNow.AddSeconds(1));
        action.ReturnValue.ShouldBeNull();
        action.ExceptionMessage.ShouldBeNull();
        action.Metadata.ShouldBeNull();
    }

    [Fact]
    public void Constructor_Should_Create_Action_With_Null_Parameters()
    {
        // Arrange
        var auditLogId = AuditLogId.NewId();

        // Act
        var action = new AuditLogAction(auditLogId, "Service", "Method", null);

        // Assert
        action.Parameters.ShouldBeNull();
    }

    [Fact]
    public void SetReturnValue_Should_Set_ReturnValue()
    {
        // Arrange
        var action = new AuditLogAction(AuditLogId.NewId(), "Service", "Method", null);

        // Act
        action.SetReturnValue("""{"id":"123"}""");

        // Assert
        action.ReturnValue.ShouldBe("""{"id":"123"}""");
    }

    [Fact]
    public void SetException_Should_Set_ExceptionMessage()
    {
        // Arrange
        var action = new AuditLogAction(AuditLogId.NewId(), "Service", "Method", null);

        // Act
        action.SetException("NullReferenceException occurred");

        // Assert
        action.ExceptionMessage.ShouldBe("NullReferenceException occurred");
    }

    [Fact]
    public void SetDuration_Should_Set_Duration()
    {
        // Arrange
        var action = new AuditLogAction(AuditLogId.NewId(), "Service", "Method", null);
        var duration = TimeSpan.FromMilliseconds(42);

        // Act
        action.SetDuration(duration);

        // Assert
        action.Duration.ShouldBe(duration);
    }

    [Fact]
    public void SetMetadata_Should_Set_Metadata()
    {
        // Arrange
        var action = new AuditLogAction(AuditLogId.NewId(), "Service", "Method", null);

        // Act
        action.SetMetadata("""{"traceId":"abc"}""");

        // Assert
        action.Metadata.ShouldBe("""{"traceId":"abc"}""");
    }
}
