using Eksen.Auditing.Entities;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Eksen.Auditing.EntityFrameworkCore.Tests;

public class EntityTypeConfigurationTests : SqliteTestBase
{
    #region AuditLog

    [Fact]
    public void AuditLog_Should_Map_To_AuditLogs_Table()
    {
        // Arrange & Act
        var entityType = DbContext.Model.FindEntityType(typeof(AuditLog));

        // Assert
        entityType.ShouldNotBeNull();
        entityType.GetTableName().ShouldBe("AuditLogs");
    }

    [Fact]
    public void AuditLog_Should_Have_Id_As_Primary_Key()
    {
        // Arrange & Act
        var entityType = DbContext.Model.FindEntityType(typeof(AuditLog));

        // Assert
        entityType.ShouldNotBeNull();
        var pk = entityType.FindPrimaryKey();
        pk.ShouldNotBeNull();
        pk.Properties.Count.ShouldBe(1);
        pk.Properties[0].Name.ShouldBe(nameof(AuditLog.Id));
    }

    [Fact]
    public void AuditLog_Should_Have_Index_On_LogTime()
    {
        // Arrange & Act
        var entityType = DbContext.Model.FindEntityType(typeof(AuditLog));

        // Assert
        entityType.ShouldNotBeNull();
        var index = entityType.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(AuditLog.LogTime)));
        index.ShouldNotBeNull();
    }

    [Fact]
    public void AuditLog_Should_Have_Index_On_UserId()
    {
        // Arrange & Act
        var entityType = DbContext.Model.FindEntityType(typeof(AuditLog));

        // Assert
        entityType.ShouldNotBeNull();
        var index = entityType.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(AuditLog.UserId)));
        index.ShouldNotBeNull();
    }

    [Fact]
    public void AuditLog_Should_Have_Index_On_CorrelationId()
    {
        // Arrange & Act
        var entityType = DbContext.Model.FindEntityType(typeof(AuditLog));

        // Assert
        entityType.ShouldNotBeNull();
        var index = entityType.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(AuditLog.CorrelationId)));
        index.ShouldNotBeNull();
    }

    [Fact]
    public void AuditLog_Should_Have_Cascade_Delete_To_Actions()
    {
        // Arrange & Act
        var entityType = DbContext.Model.FindEntityType(typeof(AuditLogAction));

        // Assert
        entityType.ShouldNotBeNull();
        var fk = entityType.GetForeignKeys()
            .FirstOrDefault(f => f.PrincipalEntityType.ClrType == typeof(AuditLog));
        fk.ShouldNotBeNull();
        fk.DeleteBehavior.ShouldBe(DeleteBehavior.Cascade);
    }

    [Fact]
    public void AuditLog_Should_Have_Cascade_Delete_To_EntityChanges()
    {
        // Arrange & Act
        var entityType = DbContext.Model.FindEntityType(typeof(AuditLogEntityChange));

        // Assert
        entityType.ShouldNotBeNull();
        var fk = entityType.GetForeignKeys()
            .FirstOrDefault(f => f.PrincipalEntityType.ClrType == typeof(AuditLog));
        fk.ShouldNotBeNull();
        fk.DeleteBehavior.ShouldBe(DeleteBehavior.Cascade);
    }

    [Fact]
    public void AuditLog_Should_Have_Cascade_Delete_To_HttpRequest()
    {
        // Arrange & Act
        var entityType = DbContext.Model.FindEntityType(typeof(AuditLogHttpRequest));

        // Assert
        entityType.ShouldNotBeNull();
        var fk = entityType.GetForeignKeys()
            .FirstOrDefault(f => f.PrincipalEntityType.ClrType == typeof(AuditLog));
        fk.ShouldNotBeNull();
        fk.DeleteBehavior.ShouldBe(DeleteBehavior.Cascade);
    }

    #endregion

    #region AuditLogAction

    [Fact]
    public void AuditLogAction_Should_Map_To_AuditLogActions_Table()
    {
        // Arrange & Act
        var entityType = DbContext.Model.FindEntityType(typeof(AuditLogAction));

        // Assert
        entityType.ShouldNotBeNull();
        entityType.GetTableName().ShouldBe("AuditLogActions");
    }

    [Fact]
    public void AuditLogAction_Should_Have_Index_On_AuditLogId()
    {
        // Arrange & Act
        var entityType = DbContext.Model.FindEntityType(typeof(AuditLogAction));

        // Assert
        entityType.ShouldNotBeNull();
        var index = entityType.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(AuditLogAction.AuditLogId)));
        index.ShouldNotBeNull();
    }

    [Fact]
    public void AuditLogAction_Should_Have_Index_On_ServiceType()
    {
        // Arrange & Act
        var entityType = DbContext.Model.FindEntityType(typeof(AuditLogAction));

        // Assert
        entityType.ShouldNotBeNull();
        var index = entityType.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(AuditLogAction.ServiceType)));
        index.ShouldNotBeNull();
    }

    #endregion

    #region AuditLogEntityChange

    [Fact]
    public void AuditLogEntityChange_Should_Map_To_AuditLogEntityChanges_Table()
    {
        // Arrange & Act
        var entityType = DbContext.Model.FindEntityType(typeof(AuditLogEntityChange));

        // Assert
        entityType.ShouldNotBeNull();
        entityType.GetTableName().ShouldBe("AuditLogEntityChanges");
    }

    [Fact]
    public void AuditLogEntityChange_Should_Have_Composite_Index_On_EntityTypeFullName_And_EntityId()
    {
        // Arrange & Act
        var entityType = DbContext.Model.FindEntityType(typeof(AuditLogEntityChange));

        // Assert
        entityType.ShouldNotBeNull();
        var index = entityType.GetIndexes()
            .FirstOrDefault(i =>
                i.Properties.Count == 2 &&
                i.Properties.Any(p => p.Name == nameof(AuditLogEntityChange.EntityTypeFullName)) &&
                i.Properties.Any(p => p.Name == nameof(AuditLogEntityChange.EntityId)));
        index.ShouldNotBeNull();
    }

    [Fact]
    public void AuditLogEntityChange_Should_Have_Cascade_Delete_To_PropertyChanges()
    {
        // Arrange & Act
        var entityType = DbContext.Model.FindEntityType(typeof(AuditLogPropertyChange));

        // Assert
        entityType.ShouldNotBeNull();
        var fk = entityType.GetForeignKeys()
            .FirstOrDefault(f => f.PrincipalEntityType.ClrType == typeof(AuditLogEntityChange));
        fk.ShouldNotBeNull();
        fk.DeleteBehavior.ShouldBe(DeleteBehavior.Cascade);
    }

    #endregion

    #region AuditLogPropertyChange

    [Fact]
    public void AuditLogPropertyChange_Should_Map_To_AuditLogPropertyChanges_Table()
    {
        // Arrange & Act
        var entityType = DbContext.Model.FindEntityType(typeof(AuditLogPropertyChange));

        // Assert
        entityType.ShouldNotBeNull();
        entityType.GetTableName().ShouldBe("AuditLogPropertyChanges");
    }

    [Fact]
    public void AuditLogPropertyChange_Should_Have_Index_On_EntityChangeId()
    {
        // Arrange & Act
        var entityType = DbContext.Model.FindEntityType(typeof(AuditLogPropertyChange));

        // Assert
        entityType.ShouldNotBeNull();
        var index = entityType.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(AuditLogPropertyChange.EntityChangeId)));
        index.ShouldNotBeNull();
    }

    #endregion

    #region AuditLogHttpRequest

    [Fact]
    public void AuditLogHttpRequest_Should_Map_To_AuditLogHttpRequests_Table()
    {
        // Arrange & Act
        var entityType = DbContext.Model.FindEntityType(typeof(AuditLogHttpRequest));

        // Assert
        entityType.ShouldNotBeNull();
        entityType.GetTableName().ShouldBe("AuditLogHttpRequests");
    }

    [Fact]
    public void AuditLogHttpRequest_Should_Have_Unique_Index_On_AuditLogId()
    {
        // Arrange & Act
        var entityType = DbContext.Model.FindEntityType(typeof(AuditLogHttpRequest));

        // Assert
        entityType.ShouldNotBeNull();
        var index = entityType.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(AuditLogHttpRequest.AuditLogId)));
        index.ShouldNotBeNull();
        index.IsUnique.ShouldBeTrue();
    }

    [Fact]
    public void AuditLogHttpRequest_Should_Have_Cascade_Delete_To_Payload()
    {
        // Arrange & Act
        var entityType = DbContext.Model.FindEntityType(typeof(AuditLogHttpRequestPayload));

        // Assert
        entityType.ShouldNotBeNull();
        var fk = entityType.GetForeignKeys()
            .FirstOrDefault(f => f.PrincipalEntityType.ClrType == typeof(AuditLogHttpRequest));
        fk.ShouldNotBeNull();
        fk.DeleteBehavior.ShouldBe(DeleteBehavior.Cascade);
    }

    #endregion

    #region AuditLogHttpRequestPayload

    [Fact]
    public void AuditLogHttpRequestPayload_Should_Map_To_AuditLogHttpRequestPayloads_Table()
    {
        // Arrange & Act
        var entityType = DbContext.Model.FindEntityType(typeof(AuditLogHttpRequestPayload));

        // Assert
        entityType.ShouldNotBeNull();
        entityType.GetTableName().ShouldBe("AuditLogHttpRequestPayloads");
    }

    [Fact]
    public void AuditLogHttpRequestPayload_Should_Have_Unique_Index_On_HttpRequestId()
    {
        // Arrange & Act
        var entityType = DbContext.Model.FindEntityType(typeof(AuditLogHttpRequestPayload));

        // Assert
        entityType.ShouldNotBeNull();
        var index = entityType.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(AuditLogHttpRequestPayload.HttpRequestId)));
        index.ShouldNotBeNull();
        index.IsUnique.ShouldBeTrue();
    }

    #endregion

    #region ModelBuilderExtensions

    [Fact]
    public void ApplyEksenAuditingConfiguration_Should_Register_All_Entity_Types()
    {
        // Arrange & Act
        var model = DbContext.Model;

        // Assert
        model.FindEntityType(typeof(AuditLog)).ShouldNotBeNull();
        model.FindEntityType(typeof(AuditLogAction)).ShouldNotBeNull();
        model.FindEntityType(typeof(AuditLogEntityChange)).ShouldNotBeNull();
        model.FindEntityType(typeof(AuditLogPropertyChange)).ShouldNotBeNull();
        model.FindEntityType(typeof(AuditLogHttpRequest)).ShouldNotBeNull();
        model.FindEntityType(typeof(AuditLogHttpRequestPayload)).ShouldNotBeNull();
    }

    #endregion
}
