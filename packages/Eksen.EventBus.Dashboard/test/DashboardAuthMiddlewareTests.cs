using System.Security.Claims;
using System.Text;
using Eksen.TestBase;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Eksen.EventBus.Dashboard.Tests;

public class DashboardAuthMiddlewareTests : EksenUnitTestBase
{
    private static DashboardAuthMiddleware CreateMiddleware(
        EventBusDashboardOptions? options = null,
        RequestDelegate? next = null)
    {
        next ??= _ => Task.CompletedTask;
        return new DashboardAuthMiddleware(
            next,
            Options.Create(options ?? new EventBusDashboardOptions()));
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        return new DefaultHttpContext
        {
            Response = { Body = new MemoryStream() }
        };
    }

    [Fact]
    public async Task InvokeAsync_Should_Allow_When_AuthMode_Is_None()
    {
        // Arrange
        var nextCalled = false;
        var middleware = CreateMiddleware(
            next: _ => { nextCalled = true; return Task.CompletedTask; });
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task InvokeAsync_Should_Reject_With_401_When_BasicAuth_Missing()
    {
        // Arrange
        var options = new EventBusDashboardOptions
        {
            Auth = new EventBusDashboardAuthOptions
            {
                AuthMode = EventBusDashboardAuthMode.BasicAuth,
                Username = "admin",
                Password = "secret"
            }
        };
        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(401);
        context.Response.Headers.WWWAuthenticate.ToString().ShouldContain("Basic");
    }

    [Fact]
    public async Task InvokeAsync_Should_Allow_When_BasicAuth_Valid()
    {
        // Arrange
        var nextCalled = false;
        var options = new EventBusDashboardOptions
        {
            Auth = new EventBusDashboardAuthOptions
            {
                AuthMode = EventBusDashboardAuthMode.BasicAuth,
                Username = "admin",
                Password = "secret"
            }
        };
        var middleware = CreateMiddleware(options, _ => { nextCalled = true; return Task.CompletedTask; });
        var context = CreateHttpContext();
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:secret"));
        context.Request.Headers.Authorization = $"Basic {credentials}";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task InvokeAsync_Should_Reject_When_BasicAuth_Invalid_Password()
    {
        // Arrange
        var options = new EventBusDashboardOptions
        {
            Auth = new EventBusDashboardAuthOptions
            {
                AuthMode = EventBusDashboardAuthMode.BasicAuth,
                Username = "admin",
                Password = "secret"
            }
        };
        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext();
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:wrong"));
        context.Request.Headers.Authorization = $"Basic {credentials}";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(401);
    }

    [Fact]
    public async Task InvokeAsync_Should_Reject_When_BasicAuth_Invalid_Username()
    {
        // Arrange
        var options = new EventBusDashboardOptions
        {
            Auth = new EventBusDashboardAuthOptions
            {
                AuthMode = EventBusDashboardAuthMode.BasicAuth,
                Username = "admin",
                Password = "secret"
            }
        };
        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext();
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes("wrong:secret"));
        context.Request.Headers.Authorization = $"Basic {credentials}";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(401);
    }

    [Fact]
    public async Task InvokeAsync_Should_Reject_When_BasicAuth_Malformed()
    {
        // Arrange
        var options = new EventBusDashboardOptions
        {
            Auth = new EventBusDashboardAuthOptions
            {
                AuthMode = EventBusDashboardAuthMode.BasicAuth,
                Username = "admin",
                Password = "secret"
            }
        };
        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext();
        context.Request.Headers.Authorization = "Basic not-valid-base64!!!";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(401);
    }

    [Fact]
    public async Task InvokeAsync_Should_Allow_OpenIdConnect_When_Authenticated()
    {
        // Arrange
        var nextCalled = false;
        var options = new EventBusDashboardOptions
        {
            Auth = new EventBusDashboardAuthOptions
            {
                AuthMode = EventBusDashboardAuthMode.OpenIdConnect
            }
        };
        var middleware = CreateMiddleware(options, _ => { nextCalled = true; return Task.CompletedTask; });
        var context = CreateHttpContext();
        var identity = new ClaimsIdentity("TestAuth");
        context.User = new ClaimsPrincipal(identity);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task InvokeAsync_Should_Reject_OpenIdConnect_When_Not_Authenticated()
    {
        // Arrange
        var options = new EventBusDashboardOptions
        {
            Auth = new EventBusDashboardAuthOptions
            {
                AuthMode = EventBusDashboardAuthMode.OpenIdConnect
            }
        };
        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(401);
    }

    [Fact]
    public async Task InvokeAsync_Should_Allow_Custom_When_Func_Returns_True()
    {
        // Arrange
        var nextCalled = false;
        var options = new EventBusDashboardOptions
        {
            Auth = new EventBusDashboardAuthOptions
            {
                AuthMode = EventBusDashboardAuthMode.Custom,
                CustomAuthorize = _ => Task.FromResult(true)
            }
        };
        var middleware = CreateMiddleware(options, _ => { nextCalled = true; return Task.CompletedTask; });
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task InvokeAsync_Should_Reject_Custom_When_Func_Returns_False()
    {
        // Arrange
        var options = new EventBusDashboardOptions
        {
            Auth = new EventBusDashboardAuthOptions
            {
                AuthMode = EventBusDashboardAuthMode.Custom,
                CustomAuthorize = _ => Task.FromResult(false)
            }
        };
        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task InvokeAsync_Should_Reject_Custom_When_Func_Is_Null()
    {
        // Arrange
        var options = new EventBusDashboardOptions
        {
            Auth = new EventBusDashboardAuthOptions
            {
                AuthMode = EventBusDashboardAuthMode.Custom,
                CustomAuthorize = null
            }
        };
        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task InvokeAsync_BasicAuth_Should_Reject_When_No_Colon_In_Credentials()
    {
        // Arrange
        var options = new EventBusDashboardOptions
        {
            Auth = new EventBusDashboardAuthOptions
            {
                AuthMode = EventBusDashboardAuthMode.BasicAuth,
                Username = "admin",
                Password = "secret"
            }
        };
        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext();
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes("nocolonhere"));
        context.Request.Headers.Authorization = $"Basic {credentials}";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(401);
    }

    [Fact]
    public async Task InvokeAsync_Should_Reject_When_Authorization_Header_Is_Bearer()
    {
        // Arrange
        var options = new EventBusDashboardOptions
        {
            Auth = new EventBusDashboardAuthOptions
            {
                AuthMode = EventBusDashboardAuthMode.BasicAuth,
                Username = "admin",
                Password = "secret"
            }
        };
        var middleware = CreateMiddleware(options);
        var context = CreateHttpContext();
        context.Request.Headers.Authorization = "Bearer some-token";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(401);
    }
}
