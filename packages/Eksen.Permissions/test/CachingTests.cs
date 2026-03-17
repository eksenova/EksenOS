using Eksen.Identity;
using Eksen.Identity.Users;
using Eksen.Permissions.Caching;
using Eksen.TestBase;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Shouldly;

namespace Eksen.Permissions.Tests;

public class InMemoryPermissionCacheTests : EksenUnitTestBase
{
    private readonly Mock<IAuthContext> _authContext;
    private readonly Mock<IPermissionStore> _permissionStore;
    private readonly IMemoryCache _memoryCache;
    private readonly InMemoryPermissionCache _sut;

    public InMemoryPermissionCacheTests()
    {
        _authContext = new Mock<IAuthContext>();
        _permissionStore = new Mock<IPermissionStore>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _sut = new InMemoryPermissionCache(
            _authContext.Object,
            new Lazy<IPermissionStore>(() => _permissionStore.Object),
            _memoryCache);
    }

    [Fact]
    public async Task GetPermissionDefinitionsAsync_Should_Cache_On_First_Call()
    {
        // Arrange
        var definitions = new List<PermissionDefinition>
        {
            new(PermissionName.Create("Orders.Create")),
            new(PermissionName.Create("Orders.Update"))
        };

        _permissionStore
            .Setup(s => s.GetPermissionDefinitionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(definitions);

        // Act
        var result1 = await _sut.GetPermissionDefinitionsAsync();
        var result2 = await _sut.GetPermissionDefinitionsAsync();

        // Assert
        result1.Count.ShouldBe(2);
        result2.Count.ShouldBe(2);

        _permissionStore.Verify(
            s => s.GetPermissionDefinitionsAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetPermissionsForUserAsync_Should_Cache_On_First_Call()
    {
        // Arrange
        var userId = new EksenUserId(System.Ulid.NewUlid());
        var permissions = new List<PermissionDefinition>
        {
            new(PermissionName.Create("Orders.Create"))
        };

        _permissionStore
            .Setup(s => s.GetUserPermissionsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions);

        // Act
        var result1 = await _sut.GetPermissionsForUserAsync(userId);
        var result2 = await _sut.GetPermissionsForUserAsync(userId);

        // Assert
        result1.Count.ShouldBe(1);
        result2.Count.ShouldBe(1);

        _permissionStore.Verify(
            s => s.GetUserPermissionsAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetPermissionsForCurrentUserAsync_Should_Return_Empty_When_No_User()
    {
        // Arrange
        _authContext.Setup(a => a.User).Returns((IAuthContextUser?)null);

        // Act
        var result = await _sut.GetPermissionsForCurrentUserAsync();

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvalidateForUserAsync_Should_Remove_Cached_Permissions()
    {
        // Arrange
        var userId = new EksenUserId(System.Ulid.NewUlid());
        var permissions = new List<PermissionDefinition>
        {
            new(PermissionName.Create("Orders.Create"))
        };

        _permissionStore
            .Setup(s => s.GetUserPermissionsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions);

        _permissionStore
            .Setup(s => s.GetPermissionDefinitionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PermissionDefinition>());

        await _sut.GetPermissionsForUserAsync(userId);

        // Act
        await _sut.InvalidateForUserAsync(userId);
        await _sut.GetPermissionsForUserAsync(userId);

        // Assert
        _permissionStore.Verify(
            s => s.GetUserPermissionsAsync(userId, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task InvalidateForPermissionDefinitionsAsync_Should_Remove_Cached_Definitions()
    {
        // Arrange
        var definitions = new List<PermissionDefinition>
        {
            new(PermissionName.Create("Orders.Create"))
        };

        _permissionStore
            .Setup(s => s.GetPermissionDefinitionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(definitions);

        await _sut.GetPermissionDefinitionsAsync();

        // Act
        await _sut.InvalidateForPermissionDefinitionsAsync();
        await _sut.GetPermissionDefinitionsAsync();

        // Assert
        _permissionStore.Verify(
            s => s.GetPermissionDefinitionsAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task InvalidateForCurrentUserAsync_Should_Do_Nothing_When_No_User()
    {
        // Arrange
        _authContext.Setup(a => a.User).Returns((IAuthContextUser?)null);

        // Act & Assert (should not throw)
        await _sut.InvalidateForCurrentUserAsync();
    }

    [Fact]
    public async Task InvalidateForCurrentUserAsync_Should_Invalidate_User_Cache()
    {
        // Arrange
        var userId = new EksenUserId(System.Ulid.NewUlid());
        var mockUser = new Mock<IAuthContextUser>();
        mockUser.Setup(u => u.UserId).Returns(userId);
        _authContext.Setup(a => a.User).Returns(mockUser.Object);

        var permissions = new List<PermissionDefinition>
        {
            new(PermissionName.Create("Orders.Create"))
        };

        _permissionStore
            .Setup(s => s.GetUserPermissionsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions);

        _permissionStore
            .Setup(s => s.GetPermissionDefinitionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PermissionDefinition>());

        await _sut.GetPermissionsForUserAsync(userId);

        // Act
        await _sut.InvalidateForCurrentUserAsync();
        await _sut.GetPermissionsForUserAsync(userId);

        // Assert
        _permissionStore.Verify(
            s => s.GetUserPermissionsAsync(userId, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task InvalidateForUserIdsAsync_Should_Remove_All_Specified_Users()
    {
        // Arrange
        var userId1 = new EksenUserId(System.Ulid.NewUlid());
        var userId2 = new EksenUserId(System.Ulid.NewUlid());

        _permissionStore
            .Setup(s => s.GetUserPermissionsAsync(It.IsAny<EksenUserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PermissionDefinition> { new(PermissionName.Create("P1")) });

        await _sut.GetPermissionsForUserAsync(userId1);
        await _sut.GetPermissionsForUserAsync(userId2);

        // Act
        await _sut.InvalidateForUserIdsAsync([userId1, userId2], CancellationToken.None);
        await _sut.GetPermissionsForUserAsync(userId1);
        await _sut.GetPermissionsForUserAsync(userId2);

        // Assert (2 initial + 2 after invalidation = 4)
        _permissionStore.Verify(
            s => s.GetUserPermissionsAsync(It.IsAny<EksenUserId>(), It.IsAny<CancellationToken>()),
            Times.Exactly(4));
    }

    [Fact]
    public async Task InvalidateForUserIdsAsync_Should_Do_Nothing_When_Empty_Collection()
    {
        // Act & Assert (should not throw)
        await _sut.InvalidateForUserIdsAsync([], CancellationToken.None);
    }
}

public class NullPermissionCacheTests : EksenUnitTestBase
{
    private readonly Mock<IAuthContext> _authContext;
    private readonly Mock<IPermissionStore> _permissionStore;
    private readonly NullPermissionCache _sut;

    public NullPermissionCacheTests()
    {
        _authContext = new Mock<IAuthContext>();
        _permissionStore = new Mock<IPermissionStore>();
        _sut = new NullPermissionCache(
            _authContext.Object,
            new Lazy<IPermissionStore>(() => _permissionStore.Object));
    }

    [Fact]
    public async Task GetPermissionDefinitionsAsync_Should_Always_Call_Store()
    {
        // Arrange
        _permissionStore
            .Setup(s => s.GetPermissionDefinitionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PermissionDefinition> { new(PermissionName.Create("P1")) });

        // Act
        await _sut.GetPermissionDefinitionsAsync();
        await _sut.GetPermissionDefinitionsAsync();

        // Assert (never caches, always calls store)
        _permissionStore.Verify(
            s => s.GetPermissionDefinitionsAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task InvalidateForUserAsync_Should_Complete_Without_Error()
    {
        // Act & Assert
        await _sut.InvalidateForUserAsync(new EksenUserId(System.Ulid.NewUlid()));
    }

    [Fact]
    public async Task InvalidateForCurrentUserAsync_Should_Complete_Without_Error()
    {
        // Act & Assert
        await _sut.InvalidateForCurrentUserAsync();
    }

    [Fact]
    public async Task InvalidateForUserIdsAsync_Should_Complete_Without_Error()
    {
        // Act & Assert
        await _sut.InvalidateForUserIdsAsync([new EksenUserId(System.Ulid.NewUlid())], CancellationToken.None);
    }

    [Fact]
    public async Task InvalidateForPermissionDefinitionsAsync_Should_Complete_Without_Error()
    {
        // Act & Assert
        await _sut.InvalidateForPermissionDefinitionsAsync();
    }
}
