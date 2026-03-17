using Eksen.Permissions.AspNetCore;
using Eksen.TestBase;
using Microsoft.AspNetCore.Authorization;
using Moq;
using Shouldly;
using System.Security.Claims;

namespace Eksen.Permissions.AspNetCore.Tests;

public class AppPermissionRequirementHandlerTests : EksenUnitTestBase
{
    private readonly Mock<IPermissionChecker> _permissionChecker;
    private readonly AppPermissionRequirementHandler _sut;

    public AppPermissionRequirementHandlerTests()
    {
        _permissionChecker = new Mock<IPermissionChecker>();
        _sut = new AppPermissionRequirementHandler(_permissionChecker.Object);
    }

    [Fact]
    public async Task HandleAsync_Should_Succeed_When_Permission_Granted()
    {
        // Arrange
        var permission = new DefinedPermission(PermissionName.Create("Orders.Create"), false);
        var requirement = new PermissionAuthorizationRequirement(permission);

        _permissionChecker
            .Setup(c => c.HasPermissionAsync(permission.Name))
            .ReturnsAsync(true);

        var user = new ClaimsPrincipal(new ClaimsIdentity());
        var context = new AuthorizationHandlerContext([requirement], user, null);

        // Act
        await _sut.HandleAsync(context);

        // Assert
        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_Should_Not_Succeed_When_Permission_Denied()
    {
        // Arrange
        var permission = new DefinedPermission(PermissionName.Create("Orders.Create"), false);
        var requirement = new PermissionAuthorizationRequirement(permission);

        _permissionChecker
            .Setup(c => c.HasPermissionAsync(permission.Name))
            .ReturnsAsync(false);

        var user = new ClaimsPrincipal(new ClaimsIdentity());
        var context = new AuthorizationHandlerContext([requirement], user, null);

        // Act
        await _sut.HandleAsync(context);

        // Assert
        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleAsync_Should_Call_PermissionChecker_With_Correct_Permission()
    {
        // Arrange
        var permission = new DefinedPermission(PermissionName.Create("Users.Manage"), false);
        var requirement = new PermissionAuthorizationRequirement(permission);

        _permissionChecker
            .Setup(c => c.HasPermissionAsync(It.IsAny<PermissionName>()))
            .ReturnsAsync(true);

        var user = new ClaimsPrincipal(new ClaimsIdentity());
        var context = new AuthorizationHandlerContext([requirement], user, null);

        // Act
        await _sut.HandleAsync(context);

        // Assert
        _permissionChecker.Verify(
            c => c.HasPermissionAsync(It.Is<PermissionName>(n => n.Value == "Users.Manage")),
            Times.Once);
    }
}
