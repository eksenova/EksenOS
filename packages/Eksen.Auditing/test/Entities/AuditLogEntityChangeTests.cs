using Eksen.Auditing.Entities;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Auditing.Tests.Entities;

public class AuditLogEntityChangeTests : EksenUnitTestBase
{
    [Theory]
    [InlineData(EntityChangeType.Created)]
    [InlineData(EntityChangeType.Updated)]
    [InlineData(EntityChangeType.Deleted)]
    public void Constructor_Should_Create_EntityChange_With_ChangeType(EntityChangeType changeType)
    {
        // Arrange
        var auditLogId = AuditLogId.NewId();
        var entityTypeFullName = "MyApp.Entities.Order";
        var entityId = "order-123";

        // Act
        var entityChange = new AuditLogEntityChange(auditLogId, changeType, entityTypeFullName, entityId);

        // Assert
        entityChange.Id.ShouldNotBeNull();
        entityChange.AuditLogId.ShouldBe(auditLogId);
        entityChange.ChangeType.ShouldBe(changeType);
        entityChange.EntityTypeFullName.ShouldBe(entityTypeFullName);
        entityChange.EntityId.ShouldBe(entityId);
        entityChange.ChangeTime.ShouldBeInRange(DateTime.UtcNow.AddSeconds(-5), DateTime.UtcNow.AddSeconds(1));
        entityChange.PropertyChanges.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_Should_Allow_Null_EntityId()
    {
        // Arrange & Act
        var entityChange = new AuditLogEntityChange(
            AuditLogId.NewId(), EntityChangeType.Created, "MyApp.Entities.Order", null);

        // Assert
        entityChange.EntityId.ShouldBeNull();
    }

    [Fact]
    public void AddPropertyChange_Should_Add_PropertyChange_To_Collection()
    {
        // Arrange
        var entityChange = new AuditLogEntityChange(
            AuditLogId.NewId(), EntityChangeType.Updated, "MyApp.Entities.Order", "order-123");

        var propertyChange = new AuditLogPropertyChange(
            entityChange.Id, "Status", "System.String", "Pending", "Completed");

        // Act
        entityChange.AddPropertyChange(propertyChange);

        // Assert
        entityChange.PropertyChanges.Count.ShouldBe(1);
        entityChange.PropertyChanges.ShouldContain(propertyChange);
    }

    [Fact]
    public void AddPropertyChange_Should_Add_Multiple_PropertyChanges()
    {
        // Arrange
        var entityChange = new AuditLogEntityChange(
            AuditLogId.NewId(), EntityChangeType.Updated, "MyApp.Entities.Order", "order-123");

        var propertyChange1 = new AuditLogPropertyChange(
            entityChange.Id, "Status", "System.String", "Pending", "Completed");

        var propertyChange2 = new AuditLogPropertyChange(
            entityChange.Id, "TotalAmount", "System.Decimal", "100.00", "200.00");

        // Act
        entityChange.AddPropertyChange(propertyChange1);
        entityChange.AddPropertyChange(propertyChange2);

        // Assert
        entityChange.PropertyChanges.Count.ShouldBe(2);
    }
}
