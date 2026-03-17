using Eksen.Identity;
using Eksen.Identity.Users;
using Eksen.Permissions.Tests.Fakes;
using Eksen.Repositories;
using Eksen.TestBase;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace Eksen.Permissions.Tests;

public class PermissionCheckerTests : EksenUnitTestBase
{
    private readonly Mock<IPermissionCache> _permissionCache;
    private readonly Mock<IEksenUserRepository<FakeUser, FakeTenant>> _userRepository;
    private readonly Mock<IAuthContext> _authContext;
    private readonly EksenPermissionOptions _options;
    private readonly PermissionChecker<FakeUser, FakeTenant> _sut;

    public PermissionCheckerTests()
    {
        _permissionCache = new Mock<IPermissionCache>();
        _userRepository = new Mock<IEksenUserRepository<FakeUser, FakeTenant>>();
        _authContext = new Mock<IAuthContext>();
        _options = new EksenPermissionOptions();

        _sut = new PermissionChecker<FakeUser, FakeTenant>(
            _permissionCache.Object,
            _userRepository.Object,
            Options.Create(_options),
            _authContext.Object);
    }

    private void SetupAuthUser(EksenUserId userId)
    {
        var mockUser = new Mock<IAuthContextUser>();
        mockUser.Setup(u => u.UserId).Returns(userId);
        _authContext.Setup(a => a.User).Returns(mockUser.Object);
    }

    [Fact]
    public async Task HasPermissionsAsync_Should_Return_True_When_All_Permissions_Granted()
    {
        // Arrange
        var permissions = new[] { PermissionName.Create("Orders.Create"), PermissionName.Create("Orders.Update") };
        var definitions = new List<PermissionDefinition>
        {
            new(PermissionName.Create("Orders.Create")),
            new(PermissionName.Create("Orders.Update"))
        };

        _permissionCache
            .Setup(c => c.GetPermissionDefinitionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(definitions);

        _permissionCache
            .Setup(c => c.GetPermissionsForCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Orders.Create", "Orders.Update" });

        // Act
        var result = await _sut.HasPermissionsAsync(permissions);

        // Assert
        result.ShouldBeTrue();

        _permissionCache.Verify(
            c => c.GetPermissionDefinitionsAsync(It.IsAny<CancellationToken>()),
            Times.Once);
        _permissionCache.Verify(
            c => c.GetPermissionsForCurrentUserAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HasPermissionsAsync_Should_Return_False_When_Missing_Granted_Permission()
    {
        // Arrange
        var permissions = new[] { PermissionName.Create("Orders.Create"), PermissionName.Create("Orders.Delete") };
        var definitions = new List<PermissionDefinition>
        {
            new(PermissionName.Create("Orders.Create")),
            new(PermissionName.Create("Orders.Delete"))
        };

        _permissionCache
            .Setup(c => c.GetPermissionDefinitionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(definitions);

        _permissionCache
            .Setup(c => c.GetPermissionsForCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Orders.Create" });

        // Act
        var result = await _sut.HasPermissionsAsync(permissions);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task HasPermissionsAsync_Should_Throw_When_Permission_Definition_Missing()
    {
        // Arrange
        var permissions = new[] { PermissionName.Create("NonExistent.Permission") };
        var definitions = new List<PermissionDefinition>
        {
            new(PermissionName.Create("Orders.Create"))
        };

        _permissionCache
            .Setup(c => c.GetPermissionDefinitionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(definitions);

        // Act & Assert
        await Should.ThrowAsync<Exception>(() => _sut.HasPermissionsAsync(permissions));
    }

    [Fact]
    public async Task HasPermissionsAsync_ByUserId_Should_Return_True_When_Permissions_Granted()
    {
        // Arrange
        var userId = new EksenUserId(System.Ulid.NewUlid());
        var permissions = new[] { PermissionName.Create("Orders.Create") };
        var definitions = new List<PermissionDefinition>
        {
            new(PermissionName.Create("Orders.Create"))
        };

        _permissionCache
            .Setup(c => c.GetPermissionDefinitionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(definitions);

        _permissionCache
            .Setup(c => c.GetPermissionsForUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Orders.Create" });

        // Act
        var result = await _sut.HasPermissionsAsync(userId, permissions);

        // Assert
        result.ShouldBeTrue();

        _permissionCache.Verify(
            c => c.GetPermissionsForUserAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HasPermissionAsync_Should_Return_False_When_User_Not_Found()
    {
        // Arrange
        var userId = new EksenUserId(System.Ulid.NewUlid());
        var permission = PermissionName.Create("Orders.Create");
        var definitions = new List<PermissionDefinition> { new(permission) };

        _permissionCache
            .Setup(c => c.GetPermissionDefinitionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(definitions);

        _permissionCache
            .Setup(c => c.GetPermissionsForCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Orders.Create" });

        SetupAuthUser(userId);

        _userRepository
            .Setup(r => r.FindByIdAsync(
                userId,
                It.IsAny<EksenUserIncludeOptions<FakeUser, FakeTenant>>(),
                It.IsAny<DefaultQueryOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((FakeUser?)null);

        // Act
        var result = await _sut.HasPermissionAsync(permission);

        // Assert
        result.ShouldBeFalse();

        _userRepository.Verify(
            r => r.FindByIdAsync(
                userId,
                It.IsAny<EksenUserIncludeOptions<FakeUser, FakeTenant>>(),
                It.IsAny<DefaultQueryOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HasPermissionAsync_Should_Return_False_When_Tenant_Is_Inactive()
    {
        // Arrange
        var userId = new EksenUserId(System.Ulid.NewUlid());
        var permission = PermissionName.Create("Orders.Create");
        var definitions = new List<PermissionDefinition> { new(permission) };
        var user = new FakeUser
        {
            Id = userId,
            IsActive = true,
            Tenant = new FakeTenant { IsActive = false }
        };

        _permissionCache
            .Setup(c => c.GetPermissionDefinitionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(definitions);

        _permissionCache
            .Setup(c => c.GetPermissionsForCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Orders.Create" });

        SetupAuthUser(userId);

        _userRepository
            .Setup(r => r.FindByIdAsync(
                userId,
                It.IsAny<EksenUserIncludeOptions<FakeUser, FakeTenant>>(),
                It.IsAny<DefaultQueryOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.HasPermissionAsync(permission);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task HasPermissionAsync_Should_Return_False_When_User_Is_Inactive()
    {
        // Arrange
        var userId = new EksenUserId(System.Ulid.NewUlid());
        var permission = PermissionName.Create("Orders.Create");
        var definitions = new List<PermissionDefinition> { new(permission) };
        var user = new FakeUser
        {
            Id = userId,
            IsActive = false,
            Tenant = new FakeTenant { IsActive = true }
        };

        _permissionCache
            .Setup(c => c.GetPermissionDefinitionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(definitions);

        _permissionCache
            .Setup(c => c.GetPermissionsForCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Orders.Create" });

        SetupAuthUser(userId);

        _userRepository
            .Setup(r => r.FindByIdAsync(
                userId,
                It.IsAny<EksenUserIncludeOptions<FakeUser, FakeTenant>>(),
                It.IsAny<DefaultQueryOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.HasPermissionAsync(permission);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task HasPermissionAsync_Should_Return_False_When_Password_Change_Required_And_Not_Allowed()
    {
        // Arrange
        var userId = new EksenUserId(System.Ulid.NewUlid());
        var permission = PermissionName.Create("Orders.Create");
        var definitions = new List<PermissionDefinition> { new(permission) };
        var user = new FakeUser
        {
            Id = userId,
            IsActive = true,
            IsPasswordChangeRequired = true,
            Tenant = new FakeTenant { IsActive = true }
        };

        _permissionCache
            .Setup(c => c.GetPermissionDefinitionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(definitions);

        _permissionCache
            .Setup(c => c.GetPermissionsForCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Orders.Create" });

        SetupAuthUser(userId);

        _userRepository
            .Setup(r => r.FindByIdAsync(
                userId,
                It.IsAny<EksenUserIncludeOptions<FakeUser, FakeTenant>>(),
                It.IsAny<DefaultQueryOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.HasPermissionAsync(permission);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task HasPermissionAsync_Should_Return_True_When_Password_Change_Required_But_Permission_Allowed()
    {
        // Arrange
        var userId = new EksenUserId(System.Ulid.NewUlid());
        var permission = PermissionName.Create("Users.ChangePassword");
        _options.PasswordChangeAllowedPermissions.Add(permission);
        var definitions = new List<PermissionDefinition> { new(permission) };
        var user = new FakeUser
        {
            Id = userId,
            IsActive = true,
            IsPasswordChangeRequired = true,
            Tenant = new FakeTenant { IsActive = true }
        };

        _permissionCache
            .Setup(c => c.GetPermissionDefinitionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(definitions);

        _permissionCache
            .Setup(c => c.GetPermissionsForCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Users.ChangePassword" });

        SetupAuthUser(userId);

        _userRepository
            .Setup(r => r.FindByIdAsync(
                userId,
                It.IsAny<EksenUserIncludeOptions<FakeUser, FakeTenant>>(),
                It.IsAny<DefaultQueryOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.HasPermissionAsync(permission);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task HasPermissionAsync_Should_Return_True_When_All_Conditions_Met()
    {
        // Arrange
        var userId = new EksenUserId(System.Ulid.NewUlid());
        var permission = PermissionName.Create("Orders.Create");
        var definitions = new List<PermissionDefinition> { new(permission) };
        var user = new FakeUser
        {
            Id = userId,
            IsActive = true,
            Tenant = new FakeTenant { IsActive = true }
        };

        _permissionCache
            .Setup(c => c.GetPermissionDefinitionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(definitions);

        _permissionCache
            .Setup(c => c.GetPermissionsForCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Orders.Create" });

        SetupAuthUser(userId);

        _userRepository
            .Setup(r => r.FindByIdAsync(
                userId,
                It.IsAny<EksenUserIncludeOptions<FakeUser, FakeTenant>>(),
                It.IsAny<DefaultQueryOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.HasPermissionAsync(permission);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task HasPermissionAsync_ByUserId_Should_Delegate_To_HasPermissionsAsync()
    {
        // Arrange
        var userId = new EksenUserId(System.Ulid.NewUlid());
        var permission = PermissionName.Create("Orders.Create");
        var definitions = new List<PermissionDefinition> { new(permission) };

        _permissionCache
            .Setup(c => c.GetPermissionDefinitionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(definitions);

        _permissionCache
            .Setup(c => c.GetPermissionsForUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Orders.Create" });

        // Act
        var result = await _sut.HasPermissionAsync(userId, permission);

        // Assert
        result.ShouldBeTrue();

        _permissionCache.Verify(
            c => c.GetPermissionsForUserAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HasPermissionAsync_Should_Return_True_When_User_Has_No_Tenant()
    {
        // Arrange
        var userId = new EksenUserId(System.Ulid.NewUlid());
        var permission = PermissionName.Create("Orders.Create");
        var definitions = new List<PermissionDefinition> { new(permission) };
        var user = new FakeUser
        {
            Id = userId,
            IsActive = true,
            Tenant = null
        };

        _permissionCache
            .Setup(c => c.GetPermissionDefinitionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(definitions);

        _permissionCache
            .Setup(c => c.GetPermissionsForCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Orders.Create" });

        SetupAuthUser(userId);

        _userRepository
            .Setup(r => r.FindByIdAsync(
                userId,
                It.IsAny<EksenUserIncludeOptions<FakeUser, FakeTenant>>(),
                It.IsAny<DefaultQueryOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.HasPermissionAsync(permission);

        // Assert
        result.ShouldBeTrue();
    }
}
