using Eksen.TestBase;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Moq;
using Shouldly;

namespace Eksen.UnitOfWork.AspNetCore.Tests;

public class UnitOfWorkMiddlewareTests : EksenUnitTestBase
{
    private readonly Mock<IUnitOfWorkManager> _unitOfWorkManager = new();

    #region Skipped Requests (No UoW)

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public async Task Invoke_Should_Skip_UoW_When_Method_Is_Ignored(string method)
    {
        // Arrange
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new UnitOfWorkMiddleware(next);
        var httpContext = CreateHttpContext(method: method, hasEndpoint: true);

        // Act
        await middleware.Invoke(httpContext, _unitOfWorkManager.Object);

        // Assert
        nextCalled.ShouldBeTrue();
        _unitOfWorkManager.Verify(
            m => m.BeginScope(
                It.IsAny<bool>(),
                It.IsAny<System.Data.IsolationLevel?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("get")]
    [InlineData("Get")]
    [InlineData("head")]
    [InlineData("Head")]
    [InlineData("options")]
    [InlineData("Options")]
    public async Task Invoke_Should_Skip_UoW_When_Method_Is_Ignored_Case_Insensitive(string method)
    {
        // Arrange
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new UnitOfWorkMiddleware(next);
        var httpContext = CreateHttpContext(method: method, hasEndpoint: true);

        // Act
        await middleware.Invoke(httpContext, _unitOfWorkManager.Object);

        // Assert
        nextCalled.ShouldBeTrue();
        _unitOfWorkManager.Verify(
            m => m.BeginScope(
                It.IsAny<bool>(),
                It.IsAny<System.Data.IsolationLevel?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Invoke_Should_Skip_UoW_When_No_Endpoint()
    {
        // Arrange
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new UnitOfWorkMiddleware(next);
        var httpContext = CreateHttpContext(method: "POST", hasEndpoint: false);

        // Act
        await middleware.Invoke(httpContext, _unitOfWorkManager.Object);

        // Assert
        nextCalled.ShouldBeTrue();
        _unitOfWorkManager.Verify(
            m => m.BeginScope(
                It.IsAny<bool>(),
                It.IsAny<System.Data.IsolationLevel?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Invoke_Should_Skip_UoW_When_Attribute_IsEnabled_Is_False()
    {
        // Arrange
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new UnitOfWorkMiddleware(next);
        var attribute = new UnitOfWorkAttribute { IsEnabled = false };
        var httpContext = CreateHttpContext(method: "POST", hasEndpoint: true, attribute: attribute);

        // Act
        await middleware.Invoke(httpContext, _unitOfWorkManager.Object);

        // Assert
        nextCalled.ShouldBeTrue();
        _unitOfWorkManager.Verify(
            m => m.BeginScope(
                It.IsAny<bool>(),
                It.IsAny<System.Data.IsolationLevel?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Normal Flow (Commit)

    [Fact]
    public async Task Invoke_Should_Begin_Scope_And_Commit_On_Success()
    {
        // Arrange
        var scope = new Mock<IUnitOfWorkScope>();
        _unitOfWorkManager
            .Setup(m => m.BeginScope(
                It.IsAny<bool>(),
                It.IsAny<System.Data.IsolationLevel?>(),
                It.IsAny<CancellationToken>()))
            .Returns(scope.Object);

        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new UnitOfWorkMiddleware(next);
        var httpContext = CreateHttpContext(method: "POST", hasEndpoint: true);

        // Act
        await middleware.Invoke(httpContext, _unitOfWorkManager.Object);

        // Assert
        nextCalled.ShouldBeTrue();

        _unitOfWorkManager.Verify(
            m => m.BeginScope(
                It.IsAny<bool>(),
                It.IsAny<System.Data.IsolationLevel?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        scope.Verify(s => s.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        scope.Verify(s => s.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
        scope.Verify(s => s.DisposeAsync(), Times.Once);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task Invoke_Should_Create_UoW_For_Mutating_Methods(string method)
    {
        // Arrange
        var scope = new Mock<IUnitOfWorkScope>();
        _unitOfWorkManager
            .Setup(m => m.BeginScope(
                It.IsAny<bool>(),
                It.IsAny<System.Data.IsolationLevel?>(),
                It.IsAny<CancellationToken>()))
            .Returns(scope.Object);

        RequestDelegate next = _ => Task.CompletedTask;

        var middleware = new UnitOfWorkMiddleware(next);
        var httpContext = CreateHttpContext(method: method, hasEndpoint: true);

        // Act
        await middleware.Invoke(httpContext, _unitOfWorkManager.Object);

        // Assert
        _unitOfWorkManager.Verify(
            m => m.BeginScope(
                It.IsAny<bool>(),
                It.IsAny<System.Data.IsolationLevel?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        scope.Verify(s => s.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Exception Flow (Rollback)

    [Fact]
    public async Task Invoke_Should_Rollback_And_Rethrow_When_Next_Throws()
    {
        // Arrange
        var scope = new Mock<IUnitOfWorkScope>();
        _unitOfWorkManager
            .Setup(m => m.BeginScope(
                It.IsAny<bool>(),
                It.IsAny<System.Data.IsolationLevel?>(),
                It.IsAny<CancellationToken>()))
            .Returns(scope.Object);

        var expectedException = new InvalidOperationException("test error");
        RequestDelegate next = _ => throw expectedException;

        var middleware = new UnitOfWorkMiddleware(next);
        var httpContext = CreateHttpContext(method: "POST", hasEndpoint: true);

        // Act & Assert
        var thrown = await Should.ThrowAsync<InvalidOperationException>(
            () => middleware.Invoke(httpContext, _unitOfWorkManager.Object));

        thrown.ShouldBe(expectedException);

        scope.Verify(s => s.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        scope.Verify(s => s.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        scope.Verify(s => s.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task Invoke_Should_Dispose_When_CommitAsync_Throws()
    {
        // Arrange
        var scope = new Mock<IUnitOfWorkScope>();
        scope
            .Setup(s => s.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("commit failed"));

        _unitOfWorkManager
            .Setup(m => m.BeginScope(
                It.IsAny<bool>(),
                It.IsAny<System.Data.IsolationLevel?>(),
                It.IsAny<CancellationToken>()))
            .Returns(scope.Object);

        RequestDelegate next = _ => Task.CompletedTask;

        var middleware = new UnitOfWorkMiddleware(next);
        var httpContext = CreateHttpContext(method: "POST", hasEndpoint: true);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            () => middleware.Invoke(httpContext, _unitOfWorkManager.Object));

        scope.Verify(s => s.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        scope.Verify(s => s.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
        scope.Verify(s => s.DisposeAsync(), Times.Once);
    }

    #endregion

    #region BeginScope Failure

    [Fact]
    public async Task Invoke_Should_Not_Dispose_Scope_When_BeginScope_Throws()
    {
        // Arrange
        var expectedException = new InvalidOperationException("provider error");
        _unitOfWorkManager
            .Setup(m => m.BeginScope(
                It.IsAny<bool>(),
                It.IsAny<System.Data.IsolationLevel?>(),
                It.IsAny<CancellationToken>()))
            .Throws(expectedException);

        RequestDelegate next = _ => Task.CompletedTask;

        var middleware = new UnitOfWorkMiddleware(next);
        var httpContext = CreateHttpContext(method: "POST", hasEndpoint: true);

        // Act & Assert
        var thrown = await Should.ThrowAsync<InvalidOperationException>(
            () => middleware.Invoke(httpContext, _unitOfWorkManager.Object));

        thrown.ShouldBe(expectedException);

        _unitOfWorkManager.Verify(
            m => m.BeginScope(
                It.IsAny<bool>(),
                It.IsAny<System.Data.IsolationLevel?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Rollback Failure

    [Fact]
    public async Task Invoke_Should_Dispose_When_Both_Next_And_Rollback_Throw()
    {
        // Arrange
        var scope = new Mock<IUnitOfWorkScope>();
        scope
            .Setup(s => s.RollbackAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("rollback failed"));

        _unitOfWorkManager
            .Setup(m => m.BeginScope(
                It.IsAny<bool>(),
                It.IsAny<System.Data.IsolationLevel?>(),
                It.IsAny<CancellationToken>()))
            .Returns(scope.Object);

        RequestDelegate next = _ => throw new InvalidOperationException("next failed");

        var middleware = new UnitOfWorkMiddleware(next);
        var httpContext = CreateHttpContext(method: "POST", hasEndpoint: true);

        // Act & Assert
        var thrown = await Should.ThrowAsync<InvalidOperationException>(
            () => middleware.Invoke(httpContext, _unitOfWorkManager.Object));

        thrown.Message.ShouldBe("rollback failed");

        scope.Verify(s => s.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        scope.Verify(s => s.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        scope.Verify(s => s.DisposeAsync(), Times.Once);
    }

    [Theory]
    [InlineData("post")]
    [InlineData("put")]
    [InlineData("patch")]
    [InlineData("delete")]
    public async Task Invoke_Should_Create_UoW_For_Lower_Case_Mutating_Methods(string method)
    {
        // Arrange
        var scope = new Mock<IUnitOfWorkScope>();
        _unitOfWorkManager
            .Setup(m => m.BeginScope(
                It.IsAny<bool>(),
                It.IsAny<System.Data.IsolationLevel?>(),
                It.IsAny<CancellationToken>()))
            .Returns(scope.Object);

        RequestDelegate next = _ => Task.CompletedTask;

        var middleware = new UnitOfWorkMiddleware(next);
        var httpContext = CreateHttpContext(method: method, hasEndpoint: true);

        // Act
        await middleware.Invoke(httpContext, _unitOfWorkManager.Object);

        // Assert
        _unitOfWorkManager.Verify(
            m => m.BeginScope(
                It.IsAny<bool>(),
                It.IsAny<System.Data.IsolationLevel?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        scope.Verify(s => s.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region UnitOfWorkAttribute IsolationLevel

    [Fact]
    public async Task Invoke_Should_Pass_IsolationLevel_From_Attribute()
    {
        // Arrange
        var scope = new Mock<IUnitOfWorkScope>();
        _unitOfWorkManager
            .Setup(m => m.BeginScope(
                It.IsAny<bool>(),
                It.IsAny<System.Data.IsolationLevel?>(),
                It.IsAny<CancellationToken>()))
            .Returns(scope.Object);

        RequestDelegate next = _ => Task.CompletedTask;

        var attribute = new UnitOfWorkAttribute
        {
            IsolationLevel = System.Data.IsolationLevel.Serializable
        };

        var middleware = new UnitOfWorkMiddleware(next);
        var httpContext = CreateHttpContext(method: "POST", hasEndpoint: true, attribute: attribute);

        // Act
        await middleware.Invoke(httpContext, _unitOfWorkManager.Object);

        // Assert
        _unitOfWorkManager.Verify(
            m => m.BeginScope(
                It.IsAny<bool>(),
                System.Data.IsolationLevel.Serializable,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Invoke_Should_Use_Default_Attribute_When_No_Attribute_On_Endpoint()
    {
        // Arrange
        var scope = new Mock<IUnitOfWorkScope>();
        _unitOfWorkManager
            .Setup(m => m.BeginScope(
                It.IsAny<bool>(),
                It.IsAny<System.Data.IsolationLevel?>(),
                It.IsAny<CancellationToken>()))
            .Returns(scope.Object);

        RequestDelegate next = _ => Task.CompletedTask;

        var middleware = new UnitOfWorkMiddleware(next);
        var httpContext = CreateHttpContext(method: "POST", hasEndpoint: true, attribute: null);

        // Act
        await middleware.Invoke(httpContext, _unitOfWorkManager.Object);

        // Assert
        _unitOfWorkManager.Verify(
            m => m.BeginScope(
                It.IsAny<bool>(),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);

        scope.Verify(s => s.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Helpers

    private static DefaultHttpContext CreateHttpContext(
        string method = "POST",
        bool hasEndpoint = true,
        UnitOfWorkAttribute? attribute = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = method;

        if (hasEndpoint)
        {
            var metadata = new EndpointMetadataCollection(
                attribute != null ? new object[] { attribute } : Array.Empty<object>());
            var endpoint = new Endpoint(_ => Task.CompletedTask, metadata, "TestEndpoint");

            var endpointFeature = new Mock<IEndpointFeature>();
            endpointFeature.Setup(f => f.Endpoint).Returns(endpoint);
            httpContext.Features.Set(endpointFeature.Object);
        }

        return httpContext;
    }

    #endregion
}
