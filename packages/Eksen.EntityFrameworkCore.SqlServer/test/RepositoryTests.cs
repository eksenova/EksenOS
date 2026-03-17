using Eksen.TestBase;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Eksen.EntityFrameworkCore.SqlServer.Tests;

public class RepositoryTests(SqlServerFixture fixture) : EksenSqlServerTestBase(fixture)
{
    [Fact]
    public async Task InsertAsync_Should_Persist_Entity()
    {
        // Arrange
        var repo = ServiceProvider.GetRequiredService<ITestEntityRepository>();
        var entity = new TestEntity(new TestEntityName("RepositoryInsertTest"));

        // Act
        await repo.InsertAsync(entity, autoSave: true);

        // Assert
        var fetched = await repo.GetAsync(entity.Id);
        fetched.ShouldNotBeNull();
        fetched.Name.ShouldBe(new TestEntityName("RepositoryInsertTest"));
    }

    [Fact]
    public async Task InsertManyAsync_Should_Persist_Multiple_Entities()
    {
        // Arrange
        var repo = ServiceProvider.GetRequiredService<ITestEntityRepository>();
        var entity1 = new TestEntity(new TestEntityName("Entity1"));
        var entity2 = new TestEntity(new TestEntityName("Entity2"));

        // Act
        await repo.InsertManyAsync([entity1, entity2], autoSave: true);

        // Assert
        var entities = await repo.GetListAsync();
        entities.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetAsync_Should_Throw_When_Entity_Not_Found()
    {
        // Arrange
        var repo = ServiceProvider.GetRequiredService<ITestEntityRepository>();
        var nonExistentId = TestEntityId.NewId();

        // Act & Assert
        await Should.ThrowAsync<Exception>(
            () => repo.GetAsync(nonExistentId));
    }

    [Fact]
    public async Task FindAsync_Should_Return_Null_When_Entity_Not_Found()
    {
        // Arrange
        var repo = ServiceProvider.GetRequiredService<ITestEntityRepository>();
        var nonExistentId = TestEntityId.NewId();

        // Act
        var result = await repo.FindAsync(nonExistentId);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAsync_Should_Soft_Delete_Entity()
    {
        // Arrange
        var repo = ServiceProvider.GetRequiredService<ITestEntityRepository>();
        var entity = new TestEntity(new TestEntityName("SoftDeleteTest"));

        await repo.InsertAsync(entity, autoSave: true);

        // Act
        await repo.DeleteAsync(entity, autoSave: true);

        // Assert
        var result = await repo.FindAsync(entity.Id);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task CountAsync_Should_Return_Correct_Count()
    {
        // Arrange
        var repo = ServiceProvider.GetRequiredService<ITestEntityRepository>();
        var entity1 = new TestEntity(new TestEntityName("Count1"));
        var entity2 = new TestEntity(new TestEntityName("Count2"));
        var entity3 = new TestEntity(new TestEntityName("Count3"));

        await repo.InsertManyAsync([entity1, entity2, entity3], autoSave: true);

        // Act
        var count = await repo.CountAsync();

        // Assert
        count.ShouldBe(3);
    }

    [Fact]
    public async Task GetListAsync_Should_Return_All_Entities()
    {
        // Arrange
        var repo = ServiceProvider.GetRequiredService<ITestEntityRepository>();
        var entity1 = new TestEntity(new TestEntityName("List1"));
        var entity2 = new TestEntity(new TestEntityName("List2"));

        await repo.InsertManyAsync([entity1, entity2], autoSave: true);

        // Act
        var entities = await repo.GetListAsync();

        // Assert
        entities.Count.ShouldBe(2);
        entities.ShouldContain(e => e.Name == new TestEntityName("List1"));
        entities.ShouldContain(e => e.Name == new TestEntityName("List2"));
    }
}
