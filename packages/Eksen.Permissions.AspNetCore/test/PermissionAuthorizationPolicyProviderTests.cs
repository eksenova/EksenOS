using Eksen.Permissions.AspNetCore;
using Eksen.TestBase;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace Eksen.Permissions.AspNetCore.Tests;

public class PermissionAuthorizationPolicyProviderTests : EksenUnitTestBase
{
    private readonly EksenPermissionOptions _permissionOptions;
    private readonly PermissionAuthorizationPolicyProvider _sut;

    public PermissionAuthorizationPolicyProviderTests()
    {
        _permissionOptions = new EksenPermissionOptions();
        _sut = new PermissionAuthorizationPolicyProvider(
            Options.Create(new AuthorizationOptions()),
            Options.Create(_permissionOptions));
    }

    [Fact]
    public async Task GetPolicyAsync_Should_Return_Policy_For_Defined_Permission()
    {
        // Arrange
        var permission = new DefinedPermission(PermissionName.Create("Orders.Create"), false);
        _permissionOptions.Permissions.Add(permission);

        // Act
        var policy = await _sut.GetPolicyAsync("Orders.Create");

        // Assert
        policy.ShouldNotBeNull();
        policy.Requirements.ShouldContain(r => r is PermissionAuthorizationRequirement);
    }

    [Fact]
    public async Task GetPolicyAsync_Should_Return_Null_For_Unknown_Permission()
    {
        // Arrange - no permissions added

        // Act
        var policy = await _sut.GetPolicyAsync("Unknown.Permission");

        // Assert
        policy.ShouldBeNull();
    }

    [Fact]
    public async Task GetPolicyAsync_Should_Match_Case_Insensitively()
    {
        // Arrange
        var permission = new DefinedPermission(PermissionName.Create("Orders.Create"), false);
        _permissionOptions.Permissions.Add(permission);

        // Act
        var policy = await _sut.GetPolicyAsync("orders.create");

        // Assert
        policy.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetPolicyAsync_Should_Include_PermissionAuthorizationRequirement()
    {
        // Arrange
        var permission = new DefinedPermission(PermissionName.Create("Users.Manage"), false);
        _permissionOptions.Permissions.Add(permission);

        // Act
        var policy = await _sut.GetPolicyAsync("Users.Manage");

        // Assert
        policy.ShouldNotBeNull();
        var requirement = policy.Requirements.OfType<PermissionAuthorizationRequirement>().Single();
        requirement.Permission.Name.Value.ShouldBe("Users.Manage");
    }
}
