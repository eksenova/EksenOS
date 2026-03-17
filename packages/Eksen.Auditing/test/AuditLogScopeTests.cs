using System.Text.Json;
using Eksen.Auditing.Entities;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Auditing.Tests;

public class AuditLogScopeTests : EksenUnitTestBase
{
    [Fact]
    public void Constructor_Should_Create_Scope_With_AuditLog()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);

        // Act
        using var scope = new AuditLogScope(auditLog);

        // Assert
        scope.AuditLog.ShouldBe(auditLog);
    }

    [Fact]
    public void AddAction_Should_Add_Action_To_AuditLog()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        using var scope = new AuditLogScope(auditLog);
        var action = new AuditLogAction(auditLog.Id, "TestService", "TestMethod", null);

        // Act
        scope.AddAction(action);

        // Assert
        auditLog.Actions.Count.ShouldBe(1);
        auditLog.Actions.ShouldContain(action);
    }

    [Fact]
    public void AddEntityChange_Should_Add_EntityChange_To_AuditLog()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        using var scope = new AuditLogScope(auditLog);
        var entityChange = new AuditLogEntityChange(
            auditLog.Id, EntityChangeType.Created, "MyApp.Entities.Order", "order-123");

        // Act
        scope.AddEntityChange(entityChange);

        // Assert
        auditLog.EntityChanges.Count.ShouldBe(1);
        auditLog.EntityChanges.ShouldContain(entityChange);
    }

    [Fact]
    public void SetMetadata_Should_Merge_Metadata_On_Dispose()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        var scope = new AuditLogScope(auditLog);

        // Act
        scope.SetMetadata("key1", "value1");
        scope.SetMetadata("key2", "value2");
        scope.Dispose();

        // Assert
        auditLog.Metadata.ShouldNotBeNull();
        var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(auditLog.Metadata!);
        metadata.ShouldNotBeNull();
        metadata.ShouldContainKeyAndValue("key1", "value1");
        metadata.ShouldContainKeyAndValue("key2", "value2");
    }

    [Fact]
    public void SetMetadata_Should_Overwrite_Duplicate_Keys()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        var scope = new AuditLogScope(auditLog);

        // Act
        scope.SetMetadata("key", "first");
        scope.SetMetadata("key", "second");
        scope.Dispose();

        // Assert
        var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(auditLog.Metadata!);
        metadata.ShouldNotBeNull();
        metadata["key"].ShouldBe("second");
    }

    [Fact]
    public void SetMetadata_Should_Merge_With_Existing_AuditLog_Metadata()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        auditLog.SetMetadata(JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["existing"] = "existingValue"
        }));

        var scope = new AuditLogScope(auditLog);

        // Act
        scope.SetMetadata("newKey", "newValue");
        scope.Dispose();

        // Assert
        var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(auditLog.Metadata!);
        metadata.ShouldNotBeNull();
        metadata.ShouldContainKeyAndValue("existing", "existingValue");
        metadata.ShouldContainKeyAndValue("newKey", "newValue");
    }

    [Fact]
    public void Dispose_Should_Not_Set_Metadata_When_No_Metadata_Was_Added()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        var scope = new AuditLogScope(auditLog);

        // Act
        scope.Dispose();

        // Assert
        auditLog.Metadata.ShouldBeNull();
    }

    [Fact]
    public void Dispose_Should_Be_Idempotent()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        var scope = new AuditLogScope(auditLog);
        scope.SetMetadata("key", "value");

        // Act
        scope.Dispose();
        scope.Dispose(); // second dispose should not throw

        // Assert
        auditLog.Metadata.ShouldNotBeNull();
    }

    [Fact]
    public void AddAction_Should_Throw_After_Dispose()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        var scope = new AuditLogScope(auditLog);
        scope.Dispose();

        var action = new AuditLogAction(auditLog.Id, "Service", "Method", null);

        // Act & Assert
        Should.Throw<ObjectDisposedException>(() => scope.AddAction(action));
    }

    [Fact]
    public void AddEntityChange_Should_Throw_After_Dispose()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        var scope = new AuditLogScope(auditLog);
        scope.Dispose();

        var entityChange = new AuditLogEntityChange(
            auditLog.Id, EntityChangeType.Created, "MyApp.Entities.Order", null);

        // Act & Assert
        Should.Throw<ObjectDisposedException>(() => scope.AddEntityChange(entityChange));
    }

    [Fact]
    public void SetMetadata_Should_Throw_After_Dispose()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        var scope = new AuditLogScope(auditLog);
        scope.Dispose();

        // Act & Assert
        Should.Throw<ObjectDisposedException>(() => scope.SetMetadata("key", "value"));
    }
}
