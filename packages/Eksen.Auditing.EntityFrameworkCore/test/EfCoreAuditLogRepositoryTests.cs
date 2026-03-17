using Eksen.Auditing.Entities;
using Eksen.Auditing.Repositories;
using Shouldly;

namespace Eksen.Auditing.EntityFrameworkCore.Tests;

public class EfCoreAuditLogRepositoryTests : SqliteTestBase
{
    private EfCoreAuditLogRepository<TestAuditDbContext> CreateRepository()
    {
        return new EfCoreAuditLogRepository<TestAuditDbContext>(DbContext);
    }

    #region ApplyQueryFilters

    [Fact]
    public async Task GetListAsync_Should_Filter_By_UserId()
    {
        // Arrange
        var userId = Eksen.Identity.Users.EksenUserId.NewId();
        var auditLog1 = new AuditLog(userId, null, null, null, null);
        var auditLog2 = new AuditLog(null, null, null, null, null);

        var repository = CreateRepository();
        await repository.InsertAsync(auditLog1, autoSave: true);
        await repository.InsertAsync(auditLog2, autoSave: true);

        var filterParameters = new AuditLogFilterParameters { UserId = userId };

        // Act
        var result = await repository.GetListAsync(filterParameters);

        // Assert
        result.Count.ShouldBe(1);
        result.First().Id.ShouldBe(auditLog1.Id);
    }

    [Fact]
    public async Task GetListAsync_Should_Filter_By_TenantId()
    {
        // Arrange
        var tenantId = Eksen.Identity.Tenants.EksenTenantId.NewId();
        var auditLog1 = new AuditLog(null, tenantId, null, null, null);
        var auditLog2 = new AuditLog(null, null, null, null, null);

        var repository = CreateRepository();
        await repository.InsertAsync(auditLog1, autoSave: true);
        await repository.InsertAsync(auditLog2, autoSave: true);

        var filterParameters = new AuditLogFilterParameters { TenantId = tenantId };

        // Act
        var result = await repository.GetListAsync(filterParameters);

        // Assert
        result.Count.ShouldBe(1);
        result.First().Id.ShouldBe(auditLog1.Id);
    }

    [Fact]
    public async Task GetListAsync_Should_Filter_By_CorrelationId()
    {
        // Arrange
        var auditLog1 = new AuditLog(null, null, null, null, "corr-001");
        var auditLog2 = new AuditLog(null, null, null, null, "corr-002");

        var repository = CreateRepository();
        await repository.InsertAsync(auditLog1, autoSave: true);
        await repository.InsertAsync(auditLog2, autoSave: true);

        var filterParameters = new AuditLogFilterParameters { CorrelationId = "corr-001" };

        // Act
        var result = await repository.GetListAsync(filterParameters);

        // Assert
        result.Count.ShouldBe(1);
        result.First().CorrelationId.ShouldBe("corr-001");
    }

    [Fact]
    public async Task GetListAsync_Should_Filter_By_FromTime()
    {
        // Arrange
        var auditLog1 = new AuditLog(null, null, null, null, null);
        await Task.Delay(10);
        var cutoff = DateTime.UtcNow;
        await Task.Delay(10);
        var auditLog2 = new AuditLog(null, null, null, null, null);

        var repository = CreateRepository();
        await repository.InsertAsync(auditLog1, autoSave: true);
        await repository.InsertAsync(auditLog2, autoSave: true);

        var filterParameters = new AuditLogFilterParameters { FromTime = cutoff };

        // Act
        var result = await repository.GetListAsync(filterParameters);

        // Assert
        result.Count.ShouldBe(1);
        result.First().Id.ShouldBe(auditLog2.Id);
    }

    [Fact]
    public async Task GetListAsync_Should_Filter_By_ToTime()
    {
        // Arrange
        var auditLog1 = new AuditLog(null, null, null, null, null);
        await Task.Delay(10);
        var cutoff = DateTime.UtcNow;
        await Task.Delay(10);
        var auditLog2 = new AuditLog(null, null, null, null, null);

        var repository = CreateRepository();
        await repository.InsertAsync(auditLog1, autoSave: true);
        await repository.InsertAsync(auditLog2, autoSave: true);

        var filterParameters = new AuditLogFilterParameters { ToTime = cutoff };

        // Act
        var result = await repository.GetListAsync(filterParameters);

        // Assert
        result.Count.ShouldBe(1);
        result.First().Id.ShouldBe(auditLog1.Id);
    }

    [Fact]
    public async Task GetListAsync_Should_Return_All_When_No_Filters()
    {
        // Arrange
        var auditLog1 = new AuditLog(null, null, null, null, null);
        var auditLog2 = new AuditLog(null, null, null, null, null);

        var repository = CreateRepository();
        await repository.InsertAsync(auditLog1, autoSave: true);
        await repository.InsertAsync(auditLog2, autoSave: true);

        // Act
        var result = await repository.GetListAsync();

        // Assert
        result.Count.ShouldBe(2);
    }

    #endregion

    #region ApplyDefaultIncludes

    [Fact]
    public async Task FindAsync_Should_Include_HttpRequest()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        var httpRequest = new AuditLogHttpRequest(
            auditLog.Id, "GET", "localhost", "/", null, "https", "HTTP/1.1", null, null);
        auditLog.SetHttpRequest(httpRequest);

        var repository = CreateRepository();
        await repository.InsertAsync(auditLog, autoSave: true);

        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.FindAsync(auditLog.Id);

        // Assert
        result.ShouldNotBeNull();
        result.HttpRequest.ShouldNotBeNull();
        result.HttpRequest.Method.ShouldBe("GET");
    }

    [Fact]
    public async Task FindAsync_Should_Include_Actions()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        auditLog.AddAction(new AuditLogAction(auditLog.Id, "SomeService", "SomeMethod", null));

        var repository = CreateRepository();
        await repository.InsertAsync(auditLog, autoSave: true);

        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.FindAsync(auditLog.Id);

        // Assert
        result.ShouldNotBeNull();
        result.Actions.Count.ShouldBe(1);
        result.Actions.First().MethodName.ShouldBe("SomeMethod");
    }

    [Fact]
    public async Task FindAsync_Should_Include_EntityChanges_With_PropertyChanges()
    {
        // Arrange
        var auditLog = new AuditLog(null, null, null, null, null);
        var entityChange = new AuditLogEntityChange(
            auditLog.Id, EntityChangeType.Created, "MyEntity", "entity-1");
        entityChange.AddPropertyChange(new AuditLogPropertyChange(
            entityChange.Id, "Name", "System.String", null, "NewValue"));
        auditLog.AddEntityChange(entityChange);

        var repository = CreateRepository();
        await repository.InsertAsync(auditLog, autoSave: true);

        DbContext.ChangeTracker.Clear();

        // Act
        var result = await repository.FindAsync(auditLog.Id);

        // Assert
        result.ShouldNotBeNull();
        result.EntityChanges.Count.ShouldBe(1);
        result.EntityChanges.First().PropertyChanges.Count.ShouldBe(1);
        result.EntityChanges.First().PropertyChanges.First().PropertyName.ShouldBe("Name");
    }

    #endregion
}
