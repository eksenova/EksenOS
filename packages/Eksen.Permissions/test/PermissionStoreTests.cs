using Eksen.Identity.Roles;
using Eksen.Identity.Users;
using Eksen.Permissions.Tests.Fakes;
using Eksen.Repositories;
using Eksen.TestBase;
using Moq;
using Shouldly;

namespace Eksen.Permissions.Tests;

public class PermissionStoreTests : EksenUnitTestBase
{
    private readonly Mock<IEksenUserPermissionRepository<FakeUser, FakeTenant>> _userPermissionRepo;
    private readonly Mock<IEksenRolePermissionRepository<FakeRole, FakeTenant>> _rolePermissionRepo;
    private readonly Mock<IEksenUserRoleRepository<FakeUser, FakeRole, FakeTenant>> _userRoleRepo;
    private readonly Mock<IEksenPermissionDefinitionRepository> _definitionRepo;
    private readonly PermissionStore<FakeUser, FakeRole, FakeTenant> _sut;

    public PermissionStoreTests()
    {
        _userPermissionRepo = new Mock<IEksenUserPermissionRepository<FakeUser, FakeTenant>>();
        _rolePermissionRepo = new Mock<IEksenRolePermissionRepository<FakeRole, FakeTenant>>();
        _userRoleRepo = new Mock<IEksenUserRoleRepository<FakeUser, FakeRole, FakeTenant>>();
        _definitionRepo = new Mock<IEksenPermissionDefinitionRepository>();
        _sut = new PermissionStore<FakeUser, FakeRole, FakeTenant>(
            _userPermissionRepo.Object,
            _rolePermissionRepo.Object,
            _userRoleRepo.Object,
            _definitionRepo.Object);
    }

    [Fact]
    public async Task GetPermissionDefinitionsAsync_Should_Return_NonDisabled_Definitions()
    {
        // Arrange
        var def1 = new PermissionDefinition(PermissionName.Create("Orders.Create"));
        var def2 = new PermissionDefinition(PermissionName.Create("Orders.Update"));

        _definitionRepo
            .Setup(r => r.GetListAsync(
                It.Is<PermissionFilterParameters>(p => p.IsDisabled == false),
                It.IsAny<DefaultIncludeOptions<PermissionDefinition>?>(),
                It.IsAny<DefaultPaginationParameters?>(),
                It.IsAny<DefaultSortingParameters<PermissionDefinition>?>(),
                It.IsAny<DefaultQueryOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([def1, def2]);

        // Act
        var result = await _sut.GetPermissionDefinitionsAsync();

        // Assert
        result.Count.ShouldBe(2);

        _definitionRepo.Verify(
            r => r.GetListAsync(
                It.Is<PermissionFilterParameters>(p => p.IsDisabled == false),
                It.IsAny<DefaultIncludeOptions<PermissionDefinition>?>(),
                It.IsAny<DefaultPaginationParameters?>(),
                It.IsAny<DefaultSortingParameters<PermissionDefinition>?>(),
                It.IsAny<DefaultQueryOptions?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetPermissionDefinitionsAsync_Should_Deduplicate_By_Name()
    {
        // Arrange
        var def1 = new PermissionDefinition(PermissionName.Create("Orders.Create"));
        var def2 = new PermissionDefinition(PermissionName.Create("Orders.Create"));

        _definitionRepo
            .Setup(r => r.GetListAsync(
                It.IsAny<PermissionFilterParameters>(),
                It.IsAny<DefaultIncludeOptions<PermissionDefinition>?>(),
                It.IsAny<DefaultPaginationParameters?>(),
                It.IsAny<DefaultSortingParameters<PermissionDefinition>?>(),
                It.IsAny<DefaultQueryOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([def1, def2]);

        // Act
        var result = await _sut.GetPermissionDefinitionsAsync();

        // Assert
        result.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetUserPermissionsAsync_Should_Merge_User_And_Role_Permissions()
    {
        // Arrange
        var userId = new EksenUserId(System.Ulid.NewUlid());
        var roleId = new EksenRoleId(System.Ulid.NewUlid());

        var userPerm = new PermissionDefinition(PermissionName.Create("Users.Manage"));
        var rolePerm = new PermissionDefinition(PermissionName.Create("Orders.Create"));

        var role = new FakeRole { Id = roleId };

        _userPermissionRepo
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PermissionDefinition> { userPerm });

        _userRoleRepo
            .Setup(r => r.GetRolesByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FakeRole> { role });

        _rolePermissionRepo
            .Setup(r => r.GetByRoleIdsAsync(
                It.Is<ICollection<EksenRoleId>>(ids => ids.Contains(roleId)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PermissionDefinition> { rolePerm });

        // Act
        var result = await _sut.GetUserPermissionsAsync(userId);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldContain(p => p.Name.Value == "Users.Manage");
        result.ShouldContain(p => p.Name.Value == "Orders.Create");

        _userPermissionRepo.Verify(
            r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);
        _userRoleRepo.Verify(
            r => r.GetRolesByUserIdAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);
        _rolePermissionRepo.Verify(
            r => r.GetByRoleIdsAsync(
                It.IsAny<ICollection<EksenRoleId>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetUserPermissionsAsync_Should_Exclude_Disabled_Permissions()
    {
        // Arrange
        var userId = new EksenUserId(System.Ulid.NewUlid());
        var enabledPerm = new PermissionDefinition(PermissionName.Create("Orders.Create"));
        var disabledPerm = new PermissionDefinition(PermissionName.Create("Orders.Delete"));
        disabledPerm.SetIsEnabled(false);

        _userPermissionRepo
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PermissionDefinition> { enabledPerm, disabledPerm });

        _userRoleRepo
            .Setup(r => r.GetRolesByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FakeRole>());

        _rolePermissionRepo
            .Setup(r => r.GetByRoleIdsAsync(
                It.IsAny<ICollection<EksenRoleId>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PermissionDefinition>());

        // Act
        var result = await _sut.GetUserPermissionsAsync(userId);

        // Assert
        result.Count.ShouldBe(1);
        result.First().Name.Value.ShouldBe("Orders.Create");
    }

    [Fact]
    public async Task GetUserPermissionsAsync_Should_Deduplicate_Across_User_And_Role_Permissions()
    {
        // Arrange
        var userId = new EksenUserId(System.Ulid.NewUlid());
        var roleId = new EksenRoleId(System.Ulid.NewUlid());
        var sharedPerm = new PermissionDefinition(PermissionName.Create("Orders.Create"));

        var role = new FakeRole { Id = roleId };

        _userPermissionRepo
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PermissionDefinition> { sharedPerm });

        _userRoleRepo
            .Setup(r => r.GetRolesByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FakeRole> { role });

        _rolePermissionRepo
            .Setup(r => r.GetByRoleIdsAsync(
                It.IsAny<ICollection<EksenRoleId>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PermissionDefinition> { sharedPerm });

        // Act
        var result = await _sut.GetUserPermissionsAsync(userId);

        // Assert
        result.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetUserPermissionsAsync_Should_Return_Empty_When_No_Permissions()
    {
        // Arrange
        var userId = new EksenUserId(System.Ulid.NewUlid());

        _userPermissionRepo
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PermissionDefinition>());

        _userRoleRepo
            .Setup(r => r.GetRolesByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FakeRole>());

        _rolePermissionRepo
            .Setup(r => r.GetByRoleIdsAsync(
                It.IsAny<ICollection<EksenRoleId>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PermissionDefinition>());

        // Act
        var result = await _sut.GetUserPermissionsAsync(userId);

        // Assert
        result.ShouldBeEmpty();
    }
}
