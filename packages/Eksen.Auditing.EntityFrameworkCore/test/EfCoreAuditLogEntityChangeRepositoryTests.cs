using Eksen.Auditing.Entities;
using Eksen.Auditing.Repositories;
using Shouldly;

namespace Eksen.Auditing.EntityFrameworkCore.Tests;

public class EfCoreAuditLogEntityChangeRepositoryTests : SqliteTestBase
{
    private EfCoreAuditLogEntityChangeRepository<TestAuditDbContext> CreateRepository()
    {
        return new EfCoreAuditLogEntityChangeRepository<TestAuditDbContext>(DbContext);
    }

    private async Task<AuditLog> CreateAuditLogAsync()
    {
        var auditLog = new AuditLog(null, null, null, null, null);
        DbContext.Set<AuditLog>().Add(auditLog);
        await DbContext.SaveChangesAsync();
        return auditLog;
    }

    #region GetByAuditLogIdAsync

    [Fact]
    public async Task GetByAuditLogIdAsync_Should_Return_EntityChanges_For_AuditLog()
    {
        // Arrange
        var auditLog = await CreateAuditLogAsync();

        var change1 = new AuditLogEntityChange(auditLog.Id, EntityChangeType.Created, "Order", "1");
        var change2 = new AuditLogEntityChange(auditLog.Id, EntityChangeType.Updated, "Product", "2");

        var otherAuditLog = await CreateAuditLogAsync();
        var otherChange = new AuditLogEntityChange(otherAuditLog.Id, EntityChangeType.Deleted, "Item", "3");

        DbContext.Set<AuditLogEntityChange>().AddRange(change1, change2, otherChange);
        await DbContext.SaveChangesAsync();

        var repository = CreateRepository();

        // Act
        var result = await repository.GetByAuditLogIdAsync(auditLog.Id);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldAllBe(c => c.AuditLogId == auditLog.Id);
    }

    [Fact]
    public async Task GetByAuditLogIdAsync_Should_Include_PropertyChanges()
    {
        // Arrange
        var auditLog = await CreateAuditLogAsync();

        var change = new AuditLogEntityChange(auditLog.Id, EntityChangeType.Updated, "Order", "1");
        change.AddPropertyChange(new AuditLogPropertyChange(
            change.Id, "Status", "System.String", "Pending", "Completed"));

        DbContext.Set<AuditLogEntityChange>().Add(change);
        await DbContext.SaveChangesAsync();

        DbContext.ChangeTracker.Clear();

        var repository = CreateRepository();

        // Act
        var result = await repository.GetByAuditLogIdAsync(auditLog.Id);

        // Assert
        result.Count.ShouldBe(1);
        result.First().PropertyChanges.Count.ShouldBe(1);
        result.First().PropertyChanges.First().PropertyName.ShouldBe("Status");
    }

    [Fact]
    public async Task GetByAuditLogIdAsync_Should_Order_By_ChangeTime()
    {
        // Arrange
        var auditLog = await CreateAuditLogAsync();

        var change1 = new AuditLogEntityChange(auditLog.Id, EntityChangeType.Created, "Order", "1");
        await Task.Delay(10);
        var change2 = new AuditLogEntityChange(auditLog.Id, EntityChangeType.Updated, "Order", "1");

        DbContext.Set<AuditLogEntityChange>().AddRange(change1, change2);
        await DbContext.SaveChangesAsync();

        var repository = CreateRepository();

        // Act
        var result = await repository.GetByAuditLogIdAsync(auditLog.Id);

        // Assert
        result.Count.ShouldBe(2);
        result.First().ChangeTime.ShouldBeLessThanOrEqualTo(result.Last().ChangeTime);
    }

    #endregion

    #region GetByEntityAsync

    [Fact]
    public async Task GetByEntityAsync_Should_Filter_By_EntityTypeFullName()
    {
        // Arrange
        var auditLog = await CreateAuditLogAsync();

        var change1 = new AuditLogEntityChange(auditLog.Id, EntityChangeType.Created, "MyApp.Order", "1");
        var change2 = new AuditLogEntityChange(auditLog.Id, EntityChangeType.Created, "MyApp.Product", "2");

        DbContext.Set<AuditLogEntityChange>().AddRange(change1, change2);
        await DbContext.SaveChangesAsync();

        var repository = CreateRepository();

        // Act
        var result = await repository.GetByEntityAsync("MyApp.Order");

        // Assert
        result.Count.ShouldBe(1);
        result.First().EntityTypeFullName.ShouldBe("MyApp.Order");
    }

    [Fact]
    public async Task GetByEntityAsync_Should_Filter_By_EntityId()
    {
        // Arrange
        var auditLog = await CreateAuditLogAsync();

        var change1 = new AuditLogEntityChange(auditLog.Id, EntityChangeType.Created, "MyApp.Order", "1");
        var change2 = new AuditLogEntityChange(auditLog.Id, EntityChangeType.Updated, "MyApp.Order", "2");

        DbContext.Set<AuditLogEntityChange>().AddRange(change1, change2);
        await DbContext.SaveChangesAsync();

        var repository = CreateRepository();

        // Act
        var result = await repository.GetByEntityAsync("MyApp.Order", "1");

        // Assert
        result.Count.ShouldBe(1);
        result.First().EntityId.ShouldBe("1");
    }

    [Fact]
    public async Task GetByEntityAsync_Should_Return_All_For_Entity_When_EntityId_Is_Null()
    {
        // Arrange
        var auditLog = await CreateAuditLogAsync();

        var change1 = new AuditLogEntityChange(auditLog.Id, EntityChangeType.Created, "MyApp.Order", "1");
        var change2 = new AuditLogEntityChange(auditLog.Id, EntityChangeType.Updated, "MyApp.Order", "2");

        DbContext.Set<AuditLogEntityChange>().AddRange(change1, change2);
        await DbContext.SaveChangesAsync();

        var repository = CreateRepository();

        // Act
        var result = await repository.GetByEntityAsync("MyApp.Order");

        // Assert
        result.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetByEntityAsync_Should_Include_PropertyChanges()
    {
        // Arrange
        var auditLog = await CreateAuditLogAsync();

        var change = new AuditLogEntityChange(auditLog.Id, EntityChangeType.Updated, "MyApp.Order", "1");
        change.AddPropertyChange(new AuditLogPropertyChange(
            change.Id, "Status", "System.String", "Old", "New"));

        DbContext.Set<AuditLogEntityChange>().Add(change);
        await DbContext.SaveChangesAsync();

        DbContext.ChangeTracker.Clear();

        var repository = CreateRepository();

        // Act
        var result = await repository.GetByEntityAsync("MyApp.Order", "1");

        // Assert
        result.Count.ShouldBe(1);
        result.First().PropertyChanges.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetByEntityAsync_Should_Order_By_ChangeTime_Descending()
    {
        // Arrange
        var auditLog = await CreateAuditLogAsync();

        var change1 = new AuditLogEntityChange(auditLog.Id, EntityChangeType.Created, "MyApp.Order", "1");
        await Task.Delay(10);
        var change2 = new AuditLogEntityChange(auditLog.Id, EntityChangeType.Updated, "MyApp.Order", "1");

        DbContext.Set<AuditLogEntityChange>().AddRange(change1, change2);
        await DbContext.SaveChangesAsync();

        var repository = CreateRepository();

        // Act
        var result = await repository.GetByEntityAsync("MyApp.Order", "1");

        // Assert
        result.Count.ShouldBe(2);
        result.First().ChangeTime.ShouldBeGreaterThanOrEqualTo(result.Last().ChangeTime);
    }

    #endregion

    #region ApplyQueryFilters

    [Fact]
    public async Task GetListAsync_Should_Filter_By_AuditLogId()
    {
        // Arrange
        var auditLog1 = await CreateAuditLogAsync();
        var auditLog2 = await CreateAuditLogAsync();

        var change1 = new AuditLogEntityChange(auditLog1.Id, EntityChangeType.Created, "Order", "1");
        var change2 = new AuditLogEntityChange(auditLog2.Id, EntityChangeType.Created, "Order", "2");

        DbContext.Set<AuditLogEntityChange>().AddRange(change1, change2);
        await DbContext.SaveChangesAsync();

        var repository = CreateRepository();
        var filterParameters = new AuditLogEntityChangeFilterParameters { AuditLogId = auditLog1.Id };

        // Act
        var result = await repository.GetListAsync(filterParameters);

        // Assert
        result.Count.ShouldBe(1);
        result.First().AuditLogId.ShouldBe(auditLog1.Id);
    }

    [Fact]
    public async Task GetListAsync_Should_Filter_By_EntityTypeFullName()
    {
        // Arrange
        var auditLog = await CreateAuditLogAsync();

        var change1 = new AuditLogEntityChange(auditLog.Id, EntityChangeType.Created, "MyApp.Order", "1");
        var change2 = new AuditLogEntityChange(auditLog.Id, EntityChangeType.Created, "MyApp.Product", "2");

        DbContext.Set<AuditLogEntityChange>().AddRange(change1, change2);
        await DbContext.SaveChangesAsync();

        var repository = CreateRepository();
        var filterParameters = new AuditLogEntityChangeFilterParameters
        {
            EntityTypeFullName = "MyApp.Order"
        };

        // Act
        var result = await repository.GetListAsync(filterParameters);

        // Assert
        result.Count.ShouldBe(1);
        result.First().EntityTypeFullName.ShouldBe("MyApp.Order");
    }

    [Fact]
    public async Task GetListAsync_Should_Filter_By_ChangeType()
    {
        // Arrange
        var auditLog = await CreateAuditLogAsync();

        var change1 = new AuditLogEntityChange(auditLog.Id, EntityChangeType.Created, "Order", "1");
        var change2 = new AuditLogEntityChange(auditLog.Id, EntityChangeType.Deleted, "Order", "2");

        DbContext.Set<AuditLogEntityChange>().AddRange(change1, change2);
        await DbContext.SaveChangesAsync();

        var repository = CreateRepository();
        var filterParameters = new AuditLogEntityChangeFilterParameters
        {
            ChangeType = EntityChangeType.Created
        };

        // Act
        var result = await repository.GetListAsync(filterParameters);

        // Assert
        result.Count.ShouldBe(1);
        result.First().ChangeType.ShouldBe(EntityChangeType.Created);
    }

    [Fact]
    public async Task GetListAsync_Should_Filter_By_EntityId()
    {
        // Arrange
        var auditLog = await CreateAuditLogAsync();

        var change1 = new AuditLogEntityChange(auditLog.Id, EntityChangeType.Created, "Order", "entity-1");
        var change2 = new AuditLogEntityChange(auditLog.Id, EntityChangeType.Created, "Order", "entity-2");

        DbContext.Set<AuditLogEntityChange>().AddRange(change1, change2);
        await DbContext.SaveChangesAsync();

        var repository = CreateRepository();
        var filterParameters = new AuditLogEntityChangeFilterParameters { EntityId = "entity-1" };

        // Act
        var result = await repository.GetListAsync(filterParameters);

        // Assert
        result.Count.ShouldBe(1);
        result.First().EntityId.ShouldBe("entity-1");
    }

    #endregion
}
