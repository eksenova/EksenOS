using Eksen.Auditing.Entities;
using Eksen.Auditing.Repositories;
using Shouldly;

namespace Eksen.Auditing.EntityFrameworkCore.Tests;

public class EfCoreAuditLogPropertyChangeRepositoryTests : SqliteTestBase
{
    private EfCoreAuditLogPropertyChangeRepository<TestAuditDbContext> CreateRepository()
    {
        return new EfCoreAuditLogPropertyChangeRepository<TestAuditDbContext>(DbContext);
    }

    private async Task<(AuditLog AuditLog, AuditLogEntityChange EntityChange)> CreateEntityChangeAsync()
    {
        var auditLog = new AuditLog(null, null, null, null, null);
        DbContext.Set<AuditLog>().Add(auditLog);
        await DbContext.SaveChangesAsync();

        var entityChange = new AuditLogEntityChange(
            auditLog.Id, EntityChangeType.Updated, "MyApp.Order", "1");
        DbContext.Set<AuditLogEntityChange>().Add(entityChange);
        await DbContext.SaveChangesAsync();

        return (auditLog, entityChange);
    }

    #region GetByEntityChangeIdAsync

    [Fact]
    public async Task GetByEntityChangeIdAsync_Should_Return_PropertyChanges_For_EntityChange()
    {
        // Arrange
        var (_, entityChange) = await CreateEntityChangeAsync();
        var (_, otherEntityChange) = await CreateEntityChangeAsync();

        var propChange1 = new AuditLogPropertyChange(
            entityChange.Id, "Status", "System.String", "Pending", "Completed");
        var propChange2 = new AuditLogPropertyChange(
            entityChange.Id, "Amount", "System.Decimal", "100", "200");
        var otherPropChange = new AuditLogPropertyChange(
            otherEntityChange.Id, "Name", "System.String", "Old", "New");

        DbContext.Set<AuditLogPropertyChange>().AddRange(propChange1, propChange2, otherPropChange);
        await DbContext.SaveChangesAsync();

        var repository = CreateRepository();

        // Act
        var result = await repository.GetByEntityChangeIdAsync(entityChange.Id);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldAllBe(p => p.EntityChangeId == entityChange.Id);
    }

    [Fact]
    public async Task GetByEntityChangeIdAsync_Should_Return_Empty_When_No_Changes()
    {
        // Arrange
        var (_, entityChange) = await CreateEntityChangeAsync();
        var repository = CreateRepository();

        // Act
        var result = await repository.GetByEntityChangeIdAsync(entityChange.Id);

        // Assert
        result.ShouldBeEmpty();
    }

    #endregion

    #region ApplyQueryFilters

    [Fact]
    public async Task GetListAsync_Should_Filter_By_EntityChangeId()
    {
        // Arrange
        var (_, entityChange1) = await CreateEntityChangeAsync();
        var (_, entityChange2) = await CreateEntityChangeAsync();

        var propChange1 = new AuditLogPropertyChange(
            entityChange1.Id, "Status", "System.String", "A", "B");
        var propChange2 = new AuditLogPropertyChange(
            entityChange2.Id, "Name", "System.String", "C", "D");

        DbContext.Set<AuditLogPropertyChange>().AddRange(propChange1, propChange2);
        await DbContext.SaveChangesAsync();

        var repository = CreateRepository();
        var filterParameters = new AuditLogPropertyChangeFilterParameters
        {
            EntityChangeId = entityChange1.Id
        };

        // Act
        var result = await repository.GetListAsync(filterParameters);

        // Assert
        result.Count.ShouldBe(1);
        result.First().EntityChangeId.ShouldBe(entityChange1.Id);
    }

    [Fact]
    public async Task GetListAsync_Should_Filter_By_PropertyName()
    {
        // Arrange
        var (_, entityChange) = await CreateEntityChangeAsync();

        var propChange1 = new AuditLogPropertyChange(
            entityChange.Id, "Status", "System.String", "A", "B");
        var propChange2 = new AuditLogPropertyChange(
            entityChange.Id, "Amount", "System.Decimal", "100", "200");

        DbContext.Set<AuditLogPropertyChange>().AddRange(propChange1, propChange2);
        await DbContext.SaveChangesAsync();

        var repository = CreateRepository();
        var filterParameters = new AuditLogPropertyChangeFilterParameters
        {
            PropertyName = "Status"
        };

        // Act
        var result = await repository.GetListAsync(filterParameters);

        // Assert
        result.Count.ShouldBe(1);
        result.First().PropertyName.ShouldBe("Status");
    }

    #endregion
}
