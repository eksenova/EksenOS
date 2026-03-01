using Eksen.TestBase;
using Eksen.UnitOfWork;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Eksen.EntityFrameworkCore.SqlServer.Tests;

public class UnitOfWorkTests(SqlServerFixture fixture) : EksenSqlServerTestBase(fixture)
{
    [Fact]
    public async Task Transactional_UnitOfWork_Should_Commit()
    {
        var unitOfWorkManager = ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var repo = ServiceProvider.GetRequiredService<ITestEntityRepository>();

        var newEntity = new TestEntity(new TestEntityName(value: "TestEntityName"));
        await using (unitOfWorkManager.BeginScope(isTransactional: true))
        {
            await repo.InsertAsync(newEntity, autoSave: true);
        }

        var entities = await repo.GetListAsync();
        var fetchedEntity = entities.Single();

        fetchedEntity.Id.ShouldBe(newEntity.Id);
        fetchedEntity.Name.ShouldBe(newEntity.Name);
    }

    [Fact]
    public async Task Transactional_UnitOfWork_Should_Rollback_On_Exception()
    {
        var unitOfWorkManager = ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var repo = ServiceProvider.GetRequiredService<ITestEntityRepository>();

        var newEntity = new TestEntity(new TestEntityName(value: "TestEntityName"));
        var newEntity2 = new TestEntity(new TestEntityName(value: "TestEntityName"));

        try
        {
            await using (unitOfWorkManager.BeginScope(isTransactional: true))
            {
                await repo.InsertAsync(newEntity, autoSave: false);
                await repo.InsertAsync(newEntity2, autoSave: true);
            }
        }
        catch (Exception)
        {
            // ignored
        }

        var entities = await repo.GetListAsync();
        entities.ShouldBeEmpty();
    }
}