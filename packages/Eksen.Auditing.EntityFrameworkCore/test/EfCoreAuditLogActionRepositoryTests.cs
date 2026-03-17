using Eksen.Auditing.Entities;
using Eksen.Auditing.Repositories;
using Shouldly;

namespace Eksen.Auditing.EntityFrameworkCore.Tests;

public class EfCoreAuditLogActionRepositoryTests : SqliteTestBase
{
    private EfCoreAuditLogActionRepository<TestAuditDbContext> CreateRepository()
    {
        return new EfCoreAuditLogActionRepository<TestAuditDbContext>(DbContext);
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
    public async Task GetByAuditLogIdAsync_Should_Return_Actions_For_AuditLog()
    {
        // Arrange
        var auditLog = await CreateAuditLogAsync();

        var action1 = new AuditLogAction(auditLog.Id, "ServiceA", "Method1", null);
        var action2 = new AuditLogAction(auditLog.Id, "ServiceB", "Method2", null);

        var otherAuditLog = await CreateAuditLogAsync();
        var otherAction = new AuditLogAction(otherAuditLog.Id, "ServiceC", "Method3", null);

        DbContext.Set<AuditLogAction>().AddRange(action1, action2, otherAction);
        await DbContext.SaveChangesAsync();

        var repository = CreateRepository();

        // Act
        var result = await repository.GetByAuditLogIdAsync(auditLog.Id);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldAllBe(a => a.AuditLogId == auditLog.Id);
    }

    [Fact]
    public async Task GetByAuditLogIdAsync_Should_Return_Empty_When_No_Actions()
    {
        // Arrange
        var auditLog = await CreateAuditLogAsync();
        var repository = CreateRepository();

        // Act
        var result = await repository.GetByAuditLogIdAsync(auditLog.Id);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetByAuditLogIdAsync_Should_Order_By_LogTime()
    {
        // Arrange
        var auditLog = await CreateAuditLogAsync();

        var action1 = new AuditLogAction(auditLog.Id, "ServiceA", "Method1", null);
        await Task.Delay(10);
        var action2 = new AuditLogAction(auditLog.Id, "ServiceB", "Method2", null);

        DbContext.Set<AuditLogAction>().AddRange(action1, action2);
        await DbContext.SaveChangesAsync();

        var repository = CreateRepository();

        // Act
        var result = await repository.GetByAuditLogIdAsync(auditLog.Id);

        // Assert
        result.Count.ShouldBe(2);
        result.First().LogTime.ShouldBeLessThanOrEqualTo(result.Last().LogTime);
    }

    #endregion

    #region ApplyQueryFilters

    [Fact]
    public async Task GetListAsync_Should_Filter_By_AuditLogId()
    {
        // Arrange
        var auditLog1 = await CreateAuditLogAsync();
        var auditLog2 = await CreateAuditLogAsync();

        var action1 = new AuditLogAction(auditLog1.Id, "ServiceA", "Method1", null);
        var action2 = new AuditLogAction(auditLog2.Id, "ServiceB", "Method2", null);

        DbContext.Set<AuditLogAction>().AddRange(action1, action2);
        await DbContext.SaveChangesAsync();

        var repository = CreateRepository();
        var filterParameters = new AuditLogActionFilterParameters { AuditLogId = auditLog1.Id };

        // Act
        var result = await repository.GetListAsync(filterParameters);

        // Assert
        result.Count.ShouldBe(1);
        result.First().AuditLogId.ShouldBe(auditLog1.Id);
    }

    [Fact]
    public async Task GetListAsync_Should_Filter_By_ServiceType()
    {
        // Arrange
        var auditLog = await CreateAuditLogAsync();

        var action1 = new AuditLogAction(auditLog.Id, "OrderService", "Create", null);
        var action2 = new AuditLogAction(auditLog.Id, "ProductService", "Get", null);

        DbContext.Set<AuditLogAction>().AddRange(action1, action2);
        await DbContext.SaveChangesAsync();

        var repository = CreateRepository();
        var filterParameters = new AuditLogActionFilterParameters { ServiceType = "Order" };

        // Act
        var result = await repository.GetListAsync(filterParameters);

        // Assert
        result.Count.ShouldBe(1);
        result.First().ServiceType.ShouldBe("OrderService");
    }

    [Fact]
    public async Task GetListAsync_Should_Filter_By_MethodName()
    {
        // Arrange
        var auditLog = await CreateAuditLogAsync();

        var action1 = new AuditLogAction(auditLog.Id, "Service", "CreateOrder", null);
        var action2 = new AuditLogAction(auditLog.Id, "Service", "DeleteOrder", null);

        DbContext.Set<AuditLogAction>().AddRange(action1, action2);
        await DbContext.SaveChangesAsync();

        var repository = CreateRepository();
        var filterParameters = new AuditLogActionFilterParameters { MethodName = "Create" };

        // Act
        var result = await repository.GetListAsync(filterParameters);

        // Assert
        result.Count.ShouldBe(1);
        result.First().MethodName.ShouldBe("CreateOrder");
    }

    #endregion
}
