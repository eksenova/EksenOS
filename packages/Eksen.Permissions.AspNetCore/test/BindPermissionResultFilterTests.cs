using Eksen.Permissions.AspNetCore;
using Eksen.TestBase;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Moq;
using Shouldly;

namespace Eksen.Permissions.AspNetCore.Tests;

public class BindPermissionResultFilterTests : EksenUnitTestBase
{
    private readonly Mock<IPermissionChecker> _permissionChecker;
    private readonly BindPermissionResultFilter _sut;

    public BindPermissionResultFilterTests()
    {
        _permissionChecker = new Mock<IPermissionChecker>();
        _sut = new BindPermissionResultFilter(_permissionChecker.Object);
    }

    private static ResultExecutingContext CreateContext(IActionResult result)
    {
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new ResultExecutingContext(actionContext, [], result, controller: new object());
    }

    [Fact]
    public async Task Should_Nullify_Property_When_Permission_Denied()
    {
        // Arrange
        var dto = new TestDto { Name = "test", Secret = "hidden" };
        var context = CreateContext(new ObjectResult(dto));
        var executed = false;

        _permissionChecker
            .Setup(c => c.HasPermissionAsync(It.Is<PermissionName>(n => n.Value == "ViewSecret")))
            .ReturnsAsync(false);

        // Act
        await _sut.OnResultExecutionAsync(context, () =>
        {
            executed = true;
            return Task.FromResult(new ResultExecutedContext(
                context, [], context.Result, context.Controller));
        });

        // Assert
        dto.Secret.ShouldBeNull();
        dto.Name.ShouldBe("test");
        executed.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Keep_Property_When_Permission_Granted()
    {
        // Arrange
        var dto = new TestDto { Name = "test", Secret = "visible" };
        var context = CreateContext(new ObjectResult(dto));

        _permissionChecker
            .Setup(c => c.HasPermissionAsync(It.Is<PermissionName>(n => n.Value == "ViewSecret")))
            .ReturnsAsync(true);

        // Act
        await _sut.OnResultExecutionAsync(context, () =>
            Task.FromResult(new ResultExecutedContext(context, [], context.Result, context.Controller)));

        // Assert
        dto.Secret.ShouldBe("visible");
    }

    [Fact]
    public async Task Should_Skip_Non_ObjectResult()
    {
        // Arrange
        var context = CreateContext(new StatusCodeResult(204));
        var executed = false;

        // Act
        await _sut.OnResultExecutionAsync(context, () =>
        {
            executed = true;
            return Task.FromResult(new ResultExecutedContext(
                context, [], context.Result, context.Controller));
        });

        // Assert
        executed.ShouldBeTrue();
        _permissionChecker.Verify(c => c.HasPermissionAsync(It.IsAny<PermissionName>()), Times.Never);
    }

    [Fact]
    public async Task Should_Skip_Error_Status_Codes()
    {
        // Arrange
        var dto = new TestDto { Name = "test", Secret = "hidden" };
        var context = CreateContext(new ObjectResult(dto) { StatusCode = 400 });

        // Act
        await _sut.OnResultExecutionAsync(context, () =>
            Task.FromResult(new ResultExecutedContext(context, [], context.Result, context.Controller)));

        // Assert
        dto.Secret.ShouldBe("hidden");
        _permissionChecker.Verify(c => c.HasPermissionAsync(It.IsAny<PermissionName>()), Times.Never);
    }

    [Fact]
    public async Task Should_Nullify_In_Nested_Object()
    {
        // Arrange
        var dto = new ParentDto
        {
            Child = new TestDto { Name = "test", Secret = "hidden" }
        };
        var context = CreateContext(new ObjectResult(dto));

        _permissionChecker
            .Setup(c => c.HasPermissionAsync(It.Is<PermissionName>(n => n.Value == "ViewSecret")))
            .ReturnsAsync(false);

        // Act
        await _sut.OnResultExecutionAsync(context, () =>
            Task.FromResult(new ResultExecutedContext(context, [], context.Result, context.Controller)));

        // Assert
        dto.Child.Secret.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Nullify_In_Collection_Items()
    {
        // Arrange
        var dto = new CollectionDto
        {
            Items = [new TestDto { Name = "test", Secret = "hidden" }]
        };
        var context = CreateContext(new ObjectResult(dto));

        _permissionChecker
            .Setup(c => c.HasPermissionAsync(It.Is<PermissionName>(n => n.Value == "ViewSecret")))
            .ReturnsAsync(false);

        // Act
        await _sut.OnResultExecutionAsync(context, () =>
            Task.FromResult(new ResultExecutedContext(context, [], context.Result, context.Controller)));

        // Assert
        dto.Items[0].Secret.ShouldBeNull();
    }

    [Fact]
    public async Task Should_Handle_Null_ObjectResult_Value()
    {
        // Arrange
        var context = CreateContext(new ObjectResult(null));
        var executed = false;

        // Act
        await _sut.OnResultExecutionAsync(context, () =>
        {
            executed = true;
            return Task.FromResult(new ResultExecutedContext(
                context, [], context.Result, context.Controller));
        });

        // Assert
        executed.ShouldBeTrue();
    }

    private class TestDto
    {
        public string? Name { get; set; }

        [BindPermission("ViewSecret")]
        public string? Secret { get; set; }
    }

    private class ParentDto
    {
        public TestDto? Child { get; set; }
    }

    private class CollectionDto
    {
        public List<TestDto> Items { get; set; } = [];
    }
}
