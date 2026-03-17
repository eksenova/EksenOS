using Eksen.Identity.AspNetCore.Tests.Fakes;
using Eksen.Identity.Roles;
using Eksen.Identity.Tenants;
using Eksen.TestBase;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace Eksen.Identity.AspNetCore.Tests;

public class EksenRoleStoreTests : EksenUnitTestBase
{
    private readonly Mock<IEksenRoleRepository<FakeRole, FakeTenant>> _roleRepositoryMock = new();
    private readonly Mock<ILogger<EksenRoleStore<FakeRole, FakeTenant>>> _loggerMock = new();
    private EksenRoleStore<FakeRole, FakeTenant> _sut;

    public EksenRoleStoreTests()
    {
        _sut = new EksenRoleStore<FakeRole, FakeTenant>(_loggerMock.Object, _roleRepositoryMock.Object);
    }

    [Fact]
    public async Task CreateAsync_Should_Return_Success_When_Insert_Succeeds()
    {
        // Arrange
        var role = new FakeRole();

        // Act
        var result = await _sut.CreateAsync(role, CancellationToken.None);

        // Assert
        result.ShouldBe(IdentityResult.Success);
        _roleRepositoryMock.Verify(r => r.InsertAsync(role, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_Should_Return_ConcurrencyFailure_When_Insert_Throws()
    {
        // Arrange
        var role = new FakeRole();
        _roleRepositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<FakeRole>(), true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB error"));

        // Act
        var result = await _sut.CreateAsync(role, CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Code == "ConcurrencyFailure");
    }

    [Fact]
    public async Task UpdateAsync_Should_Return_Success_When_Update_Succeeds()
    {
        // Arrange
        var role = new FakeRole();

        // Act
        var result = await _sut.UpdateAsync(role, CancellationToken.None);

        // Assert
        result.ShouldBe(IdentityResult.Success);
        _roleRepositoryMock.Verify(r => r.UpdateAsync(role, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_Should_Return_ConcurrencyFailure_When_Update_Throws()
    {
        // Arrange
        var role = new FakeRole();
        _roleRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<FakeRole>(), true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB error"));

        // Act
        var result = await _sut.UpdateAsync(role, CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Code == "ConcurrencyFailure");
    }

    [Fact]
    public async Task DeleteAsync_Should_Return_Success_When_Remove_Succeeds()
    {
        // Arrange
        var role = new FakeRole();

        // Act
        var result = await _sut.DeleteAsync(role, CancellationToken.None);

        // Assert
        result.ShouldBe(IdentityResult.Success);
        _roleRepositoryMock.Verify(r => r.RemoveAsync(role, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_Should_Return_ConcurrencyFailure_When_Remove_Throws()
    {
        // Arrange
        var role = new FakeRole();
        _roleRepositoryMock
            .Setup(r => r.RemoveAsync(It.IsAny<FakeRole>(), true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB error"));

        // Act
        var result = await _sut.DeleteAsync(role, CancellationToken.None);

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Code == "ConcurrencyFailure");
    }

    [Fact]
    public async Task GetRoleIdAsync_Should_Return_Ulid_String()
    {
        // Arrange
        var role = new FakeRole();

        // Act
        var result = await _sut.GetRoleIdAsync(role, CancellationToken.None);

        // Assert
        result.ShouldBe(role.Id.Value.ToString());
    }

    [Fact]
    public async Task GetRoleNameAsync_Should_Return_Role_Name_Value()
    {
        // Arrange
        var role = new FakeRole { Name = RoleName.Create("Editor") };

        // Act
        var result = await _sut.GetRoleNameAsync(role, CancellationToken.None);

        // Assert
        result.ShouldBe("Editor");
    }

    [Fact]
    public async Task SetRoleNameAsync_Should_Set_And_Update_Role_Name()
    {
        // Arrange
        var role = new FakeRole();

        // Act
        await _sut.SetRoleNameAsync(role, "NewName", CancellationToken.None);

        // Assert
        role.Name.Value.ShouldBe("NewName");
        _roleRepositoryMock.Verify(r => r.UpdateAsync(role, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetNormalizedRoleNameAsync_Should_Delegate_To_GetRoleNameAsync()
    {
        // Arrange
        var role = new FakeRole { Name = RoleName.Create("Admin") };

        // Act
        var result = await _sut.GetNormalizedRoleNameAsync(role, CancellationToken.None);

        // Assert
        result.ShouldBe("Admin");
    }

    [Fact]
    public async Task SetNormalizedRoleNameAsync_Should_Be_NoOp()
    {
        // Arrange
        var role = new FakeRole { Name = RoleName.Create("Admin") };

        // Act
        await _sut.SetNormalizedRoleNameAsync(role, "ADMIN", CancellationToken.None);

        // Assert
        role.Name.Value.ShouldBe("Admin");
        _roleRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<FakeRole>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FindByIdAsync_Should_Return_Role_When_Valid_Ulid()
    {
        // Arrange
        var role = new FakeRole();
        _roleRepositoryMock
            .Setup(r => r.FindAsync(role.Id, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);

        // Act
        var result = await _sut.FindByIdAsync(role.Id.Value.ToString(), CancellationToken.None);

        // Assert
        result.ShouldBe(role);
    }

    [Fact]
    public async Task FindByIdAsync_Should_Throw_ArgumentException_When_Invalid_Ulid()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.FindByIdAsync("invalid-ulid", CancellationToken.None));
    }

    [Fact]
    public async Task FindByNameAsync_Should_Throw_NotSupportedException()
    {
        // Act & Assert
        await Should.ThrowAsync<NotSupportedException>(
            () => _sut.FindByNameAsync("Admin", CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_When_Cancelled()
    {
        // Arrange
        var role = new FakeRole();
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            () => _sut.CreateAsync(role, cts.Token));
    }

    [Fact]
    public async Task UpdateAsync_Should_Throw_When_Cancelled()
    {
        // Arrange
        var role = new FakeRole();
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            () => _sut.UpdateAsync(role, cts.Token));
    }

    [Fact]
    public async Task DeleteAsync_Should_Throw_When_Cancelled()
    {
        // Arrange
        var role = new FakeRole();
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            () => _sut.DeleteAsync(role, cts.Token));
    }
}
