using Eksen.Auditing.Entities;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.Auditing.Tests.Entities;

public class AuditLogPropertyChangeTests : EksenUnitTestBase
{
    [Fact]
    public void Constructor_Should_Create_PropertyChange_With_All_Parameters()
    {
        // Arrange
        var entityChangeId = AuditLogEntityChangeId.NewId();
        var propertyName = "Status";
        var propertyType = "System.String";
        var originalValue = "Pending";
        var newValue = "Completed";

        // Act
        var propertyChange = new AuditLogPropertyChange(
            entityChangeId, propertyName, propertyType, originalValue, newValue);

        // Assert
        propertyChange.Id.ShouldNotBeNull();
        propertyChange.EntityChangeId.ShouldBe(entityChangeId);
        propertyChange.PropertyName.ShouldBe(propertyName);
        propertyChange.PropertyTypeFullName.ShouldBe(propertyType);
        propertyChange.OriginalValue.ShouldBe(originalValue);
        propertyChange.NewValue.ShouldBe(newValue);
    }

    [Fact]
    public void Constructor_Should_Allow_Null_Values()
    {
        // Arrange & Act
        var propertyChange = new AuditLogPropertyChange(
            AuditLogEntityChangeId.NewId(), "Name", null, null, null);

        // Assert
        propertyChange.PropertyTypeFullName.ShouldBeNull();
        propertyChange.OriginalValue.ShouldBeNull();
        propertyChange.NewValue.ShouldBeNull();
    }

    [Fact]
    public void Constructor_Should_Handle_Created_Entity_With_Null_OriginalValue()
    {
        // Arrange & Act
        var propertyChange = new AuditLogPropertyChange(
            AuditLogEntityChangeId.NewId(), "Name", "System.String", null, "NewOrder");

        // Assert
        propertyChange.OriginalValue.ShouldBeNull();
        propertyChange.NewValue.ShouldBe("NewOrder");
    }

    [Fact]
    public void Constructor_Should_Handle_Deleted_Entity_With_Null_NewValue()
    {
        // Arrange & Act
        var propertyChange = new AuditLogPropertyChange(
            AuditLogEntityChangeId.NewId(), "Name", "System.String", "OldOrder", null);

        // Assert
        propertyChange.OriginalValue.ShouldBe("OldOrder");
        propertyChange.NewValue.ShouldBeNull();
    }
}
