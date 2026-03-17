using Eksen.TestBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Eksen.EntityFrameworkCore.SqlServer.Tests;

public class AutoPropertiesIntegrationTests(SqlServerFixture fixture) : EksenSqlServerTestBase(fixture)
{
    [Fact]
    public async Task SoftDelete_Should_Mark_Entity_As_Deleted_Instead_Of_Removing()
    {
        // Arrange
        var dbContext = ServiceProvider.GetRequiredService<TestDbContext>();
        var entity = new TestEntity(new TestEntityName("SoftDeleteIntegration"));

        dbContext.TestEntities.Add(entity);
        await dbContext.SaveChangesAsync();

        // Act
        dbContext.TestEntities.Remove(entity);
        await dbContext.SaveChangesAsync();

        // Assert
        dbContext.ChangeTracker.Clear();
        var allEntities = await dbContext.TestEntities.IgnoreQueryFilters().ToListAsync();
        allEntities.Count.ShouldBe(1);
        allEntities[0].IsDeleted.ShouldBeTrue();
    }
}
