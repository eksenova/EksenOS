using Eksen.Identity.AspNetCore.Tests.Fakes;
using Eksen.Identity.Roles;
using Eksen.Identity.Tenants;
using Eksen.Identity.Users;
using Eksen.Permissions;
using Eksen.TestBase;
using Eksen.ValueObjects.Emailing;
using Eksen.ValueObjects.Hashing;
using Moq;
using Shouldly;

namespace Eksen.Identity.AspNetCore.Tests;

public class EksenUserStoreTests : EksenUnitTestBase
{
    private readonly Mock<IEksenUserRepository<FakeUser, FakeTenant>> _userRepositoryMock = new();
    private readonly Mock<IEksenUserRoleRepository<FakeUser, FakeRole, FakeTenant>> _userRoleRepositoryMock = new();
    private readonly EksenUserStore<FakeUser, FakeRole, FakeTenant> _sut;

    public EksenUserStoreTests()
    {
        _sut = new EksenUserStore<FakeUser, FakeRole, FakeTenant>(
            _userRepositoryMock.Object,
            _userRoleRepositoryMock.Object);
    }

    [Fact]
    public async Task GetEmailAsync_Should_Return_Email_Value()
    {
        // Arrange
        var user = new FakeUser { EmailAddress = EmailAddress.Parse("test@example.com") };

        // Act
        var result = await _sut.GetEmailAsync(user, CancellationToken.None);

        // Assert
        result.ShouldBe("test@example.com");
    }

    [Fact]
    public async Task GetEmailAsync_Should_Return_Null_When_No_Email()
    {
        // Arrange
        var user = new FakeUser { EmailAddress = null };

        // Act
        var result = await _sut.GetEmailAsync(user, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetNormalizedEmailAsync_Should_Return_Uppercase_Email()
    {
        // Arrange
        var user = new FakeUser { EmailAddress = EmailAddress.Parse("Test@Example.com") };

        // Act
        var result = await _sut.GetNormalizedEmailAsync(user, CancellationToken.None);

        // Assert
        result.ShouldBe("TEST@EXAMPLE.COM");
    }

    [Fact]
    public async Task GetNormalizedEmailAsync_Should_Return_Null_When_No_Email()
    {
        // Arrange
        var user = new FakeUser { EmailAddress = null };

        // Act
        var result = await _sut.GetNormalizedEmailAsync(user, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task SetNormalizedEmailAsync_Should_Throw_NotSupportedException()
    {
        // Arrange
        var user = new FakeUser();

        // Act & Assert
        await Should.ThrowAsync<NotSupportedException>(
            () => _sut.SetNormalizedEmailAsync(user, "NORM", CancellationToken.None));
    }

    [Fact]
    public async Task SetEmailAsync_Should_Throw_NotImplementedException()
    {
        // Arrange
        var user = new FakeUser();

        // Act & Assert
        await Should.ThrowAsync<NotImplementedException>(
            () => _sut.SetEmailAsync(user, "new@example.com", CancellationToken.None));
    }

    [Fact]
    public async Task GetEmailConfirmedAsync_Should_Throw_NotImplementedException()
    {
        // Arrange
        var user = new FakeUser();

        // Act & Assert
        await Should.ThrowAsync<NotImplementedException>(
            () => _sut.GetEmailConfirmedAsync(user, CancellationToken.None));
    }

    [Fact]
    public async Task SetEmailConfirmedAsync_Should_Throw_NotImplementedException()
    {
        // Arrange
        var user = new FakeUser();

        // Act & Assert
        await Should.ThrowAsync<NotImplementedException>(
            () => _sut.SetEmailConfirmedAsync(user, true, CancellationToken.None));
    }

    [Fact]
    public async Task FindByEmailAsync_Should_Return_User_When_Found()
    {
        // Arrange
        var user = new FakeUser { EmailAddress = EmailAddress.Parse("test@example.com") };
        _userRepositoryMock
            .Setup(r => r.FindByEmailAddressAsync(
                It.IsAny<EmailAddress>(),
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.FindByEmailAsync("test@example.com", CancellationToken.None);

        // Assert
        result.ShouldBe(user);
    }

    [Fact]
    public async Task FindByEmailAsync_Should_Strip_Wildcards_From_Input()
    {
        // Arrange
        EmailAddress? capturedEmail = null;
        _userRepositoryMock
            .Setup(r => r.FindByEmailAddressAsync(
                It.IsAny<EmailAddress>(),
                null,
                null,
                It.IsAny<CancellationToken>()))
            .Callback<EmailAddress, EksenUserIncludeOptions<FakeUser, FakeTenant>?, object?, CancellationToken>(
                (email, _, _, _) => capturedEmail = email)
            .ReturnsAsync((FakeUser?)null);

        // Act
        await _sut.FindByEmailAsync("%test?@example.com", CancellationToken.None);

        // Assert
        capturedEmail.ShouldNotBeNull();
        capturedEmail.Value.ShouldBe("test@example.com");
    }

    [Fact]
    public async Task SetPasswordHashAsync_Should_Set_PasswordHash_And_Update()
    {
        // Arrange
        var user = new FakeUser();

        // Act
        await _sut.SetPasswordHashAsync(user, "$2a$10$somevalidhashvalue123456789012345", CancellationToken.None);

        // Assert
        user.PasswordHash.ShouldNotBeNull();
        _userRepositoryMock.Verify(r => r.UpdateAsync(user, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetPasswordHashAsync_Should_Set_Null_When_Empty()
    {
        // Arrange
        var user = new FakeUser { PasswordHash = PasswordHash.Create("existinghash") };

        // Act
        await _sut.SetPasswordHashAsync(user, "", CancellationToken.None);

        // Assert
        user.PasswordHash.ShouldBeNull();
        _userRepositoryMock.Verify(r => r.UpdateAsync(user, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetPasswordHashAsync_Should_Set_Null_When_Whitespace()
    {
        // Arrange
        var user = new FakeUser { PasswordHash = PasswordHash.Create("existinghash") };

        // Act
        await _sut.SetPasswordHashAsync(user, "   ", CancellationToken.None);

        // Assert
        user.PasswordHash.ShouldBeNull();
    }

    [Fact]
    public async Task GetPasswordHashAsync_Should_Return_Hash_Value()
    {
        // Arrange
        var user = new FakeUser { PasswordHash = PasswordHash.Create("hashedvalue") };

        // Act
        var result = await _sut.GetPasswordHashAsync(user, CancellationToken.None);

        // Assert
        result.ShouldBe("hashedvalue");
    }

    [Fact]
    public async Task GetPasswordHashAsync_Should_Return_Null_When_No_Hash()
    {
        // Arrange
        var user = new FakeUser { PasswordHash = null };

        // Act
        var result = await _sut.GetPasswordHashAsync(user, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task HasPasswordAsync_Should_Return_True_When_Has_Hash()
    {
        // Arrange
        var user = new FakeUser { PasswordHash = PasswordHash.Create("hash") };

        // Act
        var result = await _sut.HasPasswordAsync(user, CancellationToken.None);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task HasPasswordAsync_Should_Return_False_When_No_Hash()
    {
        // Arrange
        var user = new FakeUser { PasswordHash = null };

        // Act
        var result = await _sut.HasPasswordAsync(user, CancellationToken.None);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task GetUserIdAsync_Should_Return_Ulid_String()
    {
        // Arrange
        var user = new FakeUser();

        // Act
        var result = await _sut.GetUserIdAsync(user, CancellationToken.None);

        // Assert
        result.ShouldBe(user.Id.Value.ToString());
    }

    [Fact]
    public async Task GetUserNameAsync_Should_Delegate_To_GetEmailAsync()
    {
        // Arrange
        var user = new FakeUser { EmailAddress = EmailAddress.Parse("user@test.com") };

        // Act
        var result = await _sut.GetUserNameAsync(user, CancellationToken.None);

        // Assert
        result.ShouldBe("user@test.com");
    }

    [Fact]
    public async Task SetUserNameAsync_Should_Throw_NotImplementedException()
    {
        // Arrange
        var user = new FakeUser();

        // Act & Assert (delegates to SetEmailAsync which throws)
        await Should.ThrowAsync<NotImplementedException>(
            () => _sut.SetUserNameAsync(user, "newname", CancellationToken.None));
    }

    [Fact]
    public async Task GetNormalizedUserNameAsync_Should_Return_Uppercase_Email()
    {
        // Arrange
        var user = new FakeUser { EmailAddress = EmailAddress.Parse("Test@Example.com") };

        // Act
        var result = await _sut.GetNormalizedUserNameAsync(user, CancellationToken.None);

        // Assert
        result.ShouldBe("TEST@EXAMPLE.COM");
    }

    [Fact]
    public async Task GetNormalizedUserNameAsync_Should_Return_Null_When_No_Email()
    {
        // Arrange
        var user = new FakeUser { EmailAddress = null };

        // Act
        var result = await _sut.GetNormalizedUserNameAsync(user, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task SetNormalizedUserNameAsync_Should_Throw_NotSupportedException()
    {
        // Arrange
        var user = new FakeUser();

        // Act & Assert
        await Should.ThrowAsync<NotSupportedException>(
            () => _sut.SetNormalizedUserNameAsync(user, "NORM", CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_NotImplementedException()
    {
        // Arrange
        var user = new FakeUser();

        // Act & Assert
        await Should.ThrowAsync<NotImplementedException>(
            () => _sut.CreateAsync(user, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_Should_Throw_NotImplementedException()
    {
        // Arrange
        var user = new FakeUser();

        // Act & Assert
        await Should.ThrowAsync<NotImplementedException>(
            () => _sut.UpdateAsync(user, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteAsync_Should_Throw_NotImplementedException()
    {
        // Arrange
        var user = new FakeUser();

        // Act & Assert
        await Should.ThrowAsync<NotImplementedException>(
            () => _sut.DeleteAsync(user, CancellationToken.None));
    }

    [Fact]
    public async Task FindByIdAsync_Should_Return_User_When_Valid_Ulid()
    {
        // Arrange
        var user = new FakeUser();
        _userRepositoryMock
            .Setup(r => r.FindAsync(
                user.Id,
                null,
                It.IsAny<EksenUserIncludeOptions<FakeUser, FakeTenant>>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.FindByIdAsync(user.Id.Value.ToString(), CancellationToken.None);

        // Assert
        result.ShouldBe(user);
    }

    [Fact]
    public async Task FindByIdAsync_Should_Return_Null_When_Invalid_Ulid()
    {
        // Act
        var result = await _sut.FindByIdAsync("invalid-ulid", CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task FindByIdAsync_Should_Include_Tenant()
    {
        // Arrange
        var user = new FakeUser();
        EksenUserIncludeOptions<FakeUser, FakeTenant>? capturedOptions = null;
        _userRepositoryMock
            .Setup(r => r.FindAsync(
                user.Id,
                null,
                It.IsAny<EksenUserIncludeOptions<FakeUser, FakeTenant>>(),
                null,
                It.IsAny<CancellationToken>()))
            .Callback<EksenUserId, EksenUserFilterParameters<FakeUser, FakeTenant>?, EksenUserIncludeOptions<FakeUser, FakeTenant>?, object?, CancellationToken>(
                (_, _, options, _, _) => capturedOptions = options)
            .ReturnsAsync(user);

        // Act
        await _sut.FindByIdAsync(user.Id.Value.ToString(), CancellationToken.None);

        // Assert
        capturedOptions.ShouldNotBeNull();
        capturedOptions.IncludeTenant.ShouldBeTrue();
    }

    [Fact]
    public async Task FindByNameAsync_Should_Delegate_To_FindByEmailAsync()
    {
        // Arrange
        var user = new FakeUser { EmailAddress = EmailAddress.Parse("test@example.com") };
        _userRepositoryMock
            .Setup(r => r.FindByEmailAddressAsync(
                It.IsAny<EmailAddress>(),
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.FindByNameAsync("test@example.com", CancellationToken.None);

        // Assert
        result.ShouldBe(user);
    }

    [Fact]
    public async Task GetRolesAsync_Should_Return_Role_Names()
    {
        // Arrange
        var user = new FakeUser();
        var roles = new List<FakeRole>
        {
            new() { Name = RoleName.Create("Admin") },
            new() { Name = RoleName.Create("Editor") }
        };
        _userRoleRepositoryMock
            .Setup(r => r.GetRolesByUserIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(roles);

        // Act
        var result = await _sut.GetRolesAsync(user, CancellationToken.None);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldContain("Admin");
        result.ShouldContain("Editor");
    }

    [Fact]
    public async Task IsInRoleAsync_Should_Return_True_When_User_Has_Role()
    {
        // Arrange
        var user = new FakeUser();
        var roles = new List<FakeRole> { new() { Name = RoleName.Create("Admin") } };
        _userRoleRepositoryMock
            .Setup(r => r.GetRolesByUserIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(roles);

        // Act
        var result = await _sut.IsInRoleAsync(user, "Admin", CancellationToken.None);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task IsInRoleAsync_Should_Return_False_When_User_Not_In_Role()
    {
        // Arrange
        var user = new FakeUser();
        var roles = new List<FakeRole> { new() { Name = RoleName.Create("Editor") } };
        _userRoleRepositoryMock
            .Setup(r => r.GetRolesByUserIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(roles);

        // Act
        var result = await _sut.IsInRoleAsync(user, "Admin", CancellationToken.None);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task AddToRoleAsync_Should_Throw_NotImplementedException()
    {
        // Arrange
        var user = new FakeUser();

        // Act & Assert
        await Should.ThrowAsync<NotImplementedException>(
            () => _sut.AddToRoleAsync(user, "Admin", CancellationToken.None));
    }

    [Fact]
    public async Task RemoveFromRoleAsync_Should_Throw_NotImplementedException()
    {
        // Arrange
        var user = new FakeUser();

        // Act & Assert
        await Should.ThrowAsync<NotImplementedException>(
            () => _sut.RemoveFromRoleAsync(user, "Admin", CancellationToken.None));
    }

    [Fact]
    public async Task GetUsersInRoleAsync_Should_Throw_NotImplementedException()
    {
        // Act & Assert
        await Should.ThrowAsync<NotImplementedException>(
            () => _sut.GetUsersInRoleAsync("Admin", CancellationToken.None));
    }
}
