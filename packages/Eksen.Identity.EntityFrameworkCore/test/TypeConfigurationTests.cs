using Eksen.Identity.EntityFrameworkCore.Tests.Fakes;
using Eksen.Identity.Roles;
using Eksen.Identity.Tenants;
using Eksen.TestBase;
using Eksen.ValueObjects.Emailing;
using Eksen.ValueObjects.Hashing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Eksen.Identity.EntityFrameworkCore.Tests;

public class TypeConfigurationTests : EksenUnitTestBase, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IdentityTestDbContext _dbContext;

    public TypeConfigurationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<IdentityTestDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new IdentityTestDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void Tenant_Id_Should_Be_Configured_As_Key()
    {
        // Arrange
        var entityType = _dbContext.Model.FindEntityType(typeof(TestTenant));

        // Assert
        entityType.ShouldNotBeNull();
        var key = entityType.FindPrimaryKey();
        key.ShouldNotBeNull();
        key.Properties.ShouldHaveSingleItem().Name.ShouldBe("Id");
    }

    [Fact]
    public void Tenant_Name_Should_Have_MaxLength()
    {
        // Arrange
        var entityType = _dbContext.Model.FindEntityType(typeof(TestTenant));

        // Assert
        entityType.ShouldNotBeNull();
        var nameProperty = entityType.FindProperty("Name");
        nameProperty.ShouldNotBeNull();
        nameProperty.GetMaxLength().ShouldBe(TenantName.MaxLength);
    }

    [Fact]
    public void Tenant_Should_Have_Unique_Index_On_Name()
    {
        // Arrange
        var entityType = _dbContext.Model.FindEntityType(typeof(TestTenant));

        // Assert
        entityType.ShouldNotBeNull();
        var indexes = entityType.GetIndexes().ToList();
        indexes.ShouldContain(idx =>
            idx.Properties.Any(p => p.Name == "Name") && idx.IsUnique);
    }

    [Fact]
    public void Role_Id_Should_Be_Configured_As_Key()
    {
        // Arrange
        var entityType = _dbContext.Model.FindEntityType(typeof(TestRole));

        // Assert
        entityType.ShouldNotBeNull();
        var key = entityType.FindPrimaryKey();
        key.ShouldNotBeNull();
        key.Properties.ShouldHaveSingleItem().Name.ShouldBe("Id");
    }

    [Fact]
    public void Role_Name_Should_Have_MaxLength()
    {
        // Arrange
        var entityType = _dbContext.Model.FindEntityType(typeof(TestRole));

        // Assert
        entityType.ShouldNotBeNull();
        var nameProperty = entityType.FindProperty("Name");
        nameProperty.ShouldNotBeNull();
        nameProperty.GetMaxLength().ShouldBe(RoleName.MaxLength);
    }

    [Fact]
    public void Role_Should_Have_Unique_Indexes()
    {
        // Arrange
        var entityType = _dbContext.Model.FindEntityType(typeof(TestRole));

        // Assert
        entityType.ShouldNotBeNull();
        var indexes = entityType.GetIndexes().ToList();

        // Should have at least one unique index involving Name
        indexes.ShouldContain(idx =>
            idx.Properties.Any(p => p.Name == "Name") && idx.IsUnique);
    }

    [Fact]
    public void Role_Should_Have_Foreign_Key_To_Tenant()
    {
        // Arrange
        var entityType = _dbContext.Model.FindEntityType(typeof(TestRole));

        // Assert
        entityType.ShouldNotBeNull();
        var fk = entityType.GetForeignKeys().FirstOrDefault();
        fk.ShouldNotBeNull();
        fk.PrincipalEntityType.ClrType.ShouldBe(typeof(TestTenant));
    }

    [Fact]
    public void User_Id_Should_Be_Configured_As_Key()
    {
        // Arrange
        var entityType = _dbContext.Model.FindEntityType(typeof(TestUser));

        // Assert
        entityType.ShouldNotBeNull();
        var key = entityType.FindPrimaryKey();
        key.ShouldNotBeNull();
        key.Properties.ShouldHaveSingleItem().Name.ShouldBe("Id");
    }

    [Fact]
    public void User_EmailAddress_Should_Have_MaxLength()
    {
        // Arrange
        var entityType = _dbContext.Model.FindEntityType(typeof(TestUser));

        // Assert
        entityType.ShouldNotBeNull();
        var emailProperty = entityType.FindProperty("EmailAddress");
        emailProperty.ShouldNotBeNull();
        emailProperty.GetMaxLength().ShouldBe(EmailAddress.MaxLength);
    }

    [Fact]
    public void User_PasswordHash_Should_Have_MaxLength()
    {
        // Arrange
        var entityType = _dbContext.Model.FindEntityType(typeof(TestUser));

        // Assert
        entityType.ShouldNotBeNull();
        var hashProperty = entityType.FindProperty("PasswordHash");
        hashProperty.ShouldNotBeNull();
        hashProperty.GetMaxLength().ShouldBe(PasswordHash.MaxLength);
    }

    [Fact]
    public void User_Should_Have_Unique_Indexes_On_EmailAddress()
    {
        // Arrange
        var entityType = _dbContext.Model.FindEntityType(typeof(TestUser));

        // Assert
        entityType.ShouldNotBeNull();
        var indexes = entityType.GetIndexes().ToList();
        indexes.ShouldContain(idx =>
            idx.Properties.Any(p => p.Name == "EmailAddress") && idx.IsUnique);
    }

    [Fact]
    public void User_Should_Have_Foreign_Key_To_Tenant()
    {
        // Arrange
        var entityType = _dbContext.Model.FindEntityType(typeof(TestUser));

        // Assert
        entityType.ShouldNotBeNull();
        var fk = entityType.GetForeignKeys().FirstOrDefault();
        fk.ShouldNotBeNull();
        fk.PrincipalEntityType.ClrType.ShouldBe(typeof(TestTenant));
    }

    [Fact]
    public void Role_Tenant_Relationship_Should_Restrict_Delete()
    {
        // Arrange
        var entityType = _dbContext.Model.FindEntityType(typeof(TestRole));

        // Assert
        entityType.ShouldNotBeNull();
        var fk = entityType.GetForeignKeys().FirstOrDefault();
        fk.ShouldNotBeNull();
        fk.DeleteBehavior.ShouldBe(DeleteBehavior.Restrict);
    }

    [Fact]
    public void User_Tenant_Relationship_Should_Restrict_Delete()
    {
        // Arrange
        var entityType = _dbContext.Model.FindEntityType(typeof(TestUser));

        // Assert
        entityType.ShouldNotBeNull();
        var fk = entityType.GetForeignKeys().FirstOrDefault();
        fk.ShouldNotBeNull();
        fk.DeleteBehavior.ShouldBe(DeleteBehavior.Restrict);
    }
}
