using System.Security.Claims;
using Eksen.Identity.AspNetCore.Authentication;
using Eksen.Identity.Claims;
using Eksen.TestBase;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace Eksen.Identity.AspNetCore.Tests;

public class ClaimsBasedAuthContextTests : EksenUnitTestBase
{
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock = new();
    private readonly IOptions<IdentityOptions> _identityOptions = Options.Create(new IdentityOptions());

    private ClaimsBasedAuthContext CreateSut() =>
        new(_httpContextAccessorMock.Object, _identityOptions);

    private void SetupHttpContextWithClaims(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        _httpContextAccessorMock.Setup(a => a.HttpContext).Returns(httpContext);
    }

    private ClaimsPrincipal CreateAuthenticatedPrincipal(params Claim[] claims)
    {
        var allClaims = new List<Claim>
        {
            new(_identityOptions.Value.ClaimsIdentity.UserIdClaimType, System.Ulid.NewUlid().ToString()),
            new(_identityOptions.Value.ClaimsIdentity.UserNameClaimType, "Test User"),
            new(_identityOptions.Value.ClaimsIdentity.EmailClaimType, "test@example.com")
        };
        allClaims.AddRange(claims);
        return new ClaimsPrincipal(new ClaimsIdentity(allClaims, "TestAuth"));
    }

    [Fact]
    public void IsAuthenticated_Should_Return_False_When_No_HttpContext()
    {
        // Arrange
        _httpContextAccessorMock.Setup(a => a.HttpContext).Returns((HttpContext?)null);
        var sut = CreateSut();

        // Act & Assert
        sut.IsAuthenticated.ShouldBeFalse();
    }

    [Fact]
    public void IsAuthenticated_Should_Return_False_When_No_User_Claims()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        _httpContextAccessorMock.Setup(a => a.HttpContext).Returns(httpContext);
        var sut = CreateSut();

        // Act & Assert
        sut.IsAuthenticated.ShouldBeFalse();
    }

    [Fact]
    public void IsAuthenticated_Should_Return_True_When_User_Has_Required_Claims()
    {
        // Arrange
        var httpContext = new DefaultHttpContext
        {
            User = CreateAuthenticatedPrincipal()
        };
        _httpContextAccessorMock.Setup(a => a.HttpContext).Returns(httpContext);
        var sut = CreateSut();

        // Act & Assert
        sut.IsAuthenticated.ShouldBeTrue();
    }

    [Fact]
    public void User_Should_Return_Null_When_No_HttpContext()
    {
        // Arrange
        _httpContextAccessorMock.Setup(a => a.HttpContext).Returns((HttpContext?)null);
        var sut = CreateSut();

        // Act & Assert
        sut.User.ShouldBeNull();
    }

    [Fact]
    public void User_Should_Return_AuthContextUser_When_Valid_Claims()
    {
        // Arrange
        var userId = System.Ulid.NewUlid();
        SetupHttpContextWithClaims(
            new Claim(_identityOptions.Value.ClaimsIdentity.UserIdClaimType, userId.ToString()),
            new Claim(_identityOptions.Value.ClaimsIdentity.UserNameClaimType, "John Doe"),
            new Claim(_identityOptions.Value.ClaimsIdentity.EmailClaimType, "john@example.com")
        );
        var sut = CreateSut();

        // Act
        var user = sut.User;

        // Assert
        user.ShouldNotBeNull();
        user.UserId.ShouldNotBeNull();
        user.UserId!.Value.ShouldBe(userId);
        user.EmailAddress.ShouldNotBeNull();
        user.EmailAddress!.Value.ShouldBe("john@example.com");
    }

    [Fact]
    public void User_Should_Return_Null_When_Missing_UserId_Claim()
    {
        // Arrange
        SetupHttpContextWithClaims(
            new Claim(_identityOptions.Value.ClaimsIdentity.UserNameClaimType, "John Doe"),
            new Claim(_identityOptions.Value.ClaimsIdentity.EmailClaimType, "john@example.com")
        );
        var sut = CreateSut();

        // Act & Assert
        sut.User.ShouldBeNull();
    }

    [Fact]
    public void Tenant_Should_Return_Null_When_No_HttpContext()
    {
        // Arrange
        _httpContextAccessorMock.Setup(a => a.HttpContext).Returns((HttpContext?)null);
        var sut = CreateSut();

        // Act & Assert
        sut.Tenant.ShouldBeNull();
    }

    [Fact]
    public void Tenant_Should_Return_AuthContextTenant_When_Valid_Claims()
    {
        // Arrange
        var tenantId = System.Ulid.NewUlid();
        SetupHttpContextWithClaims(
            new Claim(EksenClaims.TenantId, tenantId.ToString()),
            new Claim(EksenClaims.TenantName, "Acme Corp")
        );
        var sut = CreateSut();

        // Act
        var tenant = sut.Tenant;

        // Assert
        tenant.ShouldNotBeNull();
        tenant.TenantId.Value.ShouldBe(tenantId);
        tenant.TenantName.Value.ShouldBe("Acme Corp");
    }

    [Fact]
    public void Tenant_Should_Return_Null_When_Missing_TenantId_Claim()
    {
        // Arrange
        SetupHttpContextWithClaims(
            new Claim(EksenClaims.TenantName, "Acme Corp")
        );
        var sut = CreateSut();

        // Act & Assert
        sut.Tenant.ShouldBeNull();
    }

    [Fact]
    public void Tenant_Should_Return_Null_When_Invalid_TenantId()
    {
        // Arrange
        SetupHttpContextWithClaims(
            new Claim(EksenClaims.TenantId, "not-a-ulid"),
            new Claim(EksenClaims.TenantName, "Acme Corp")
        );
        var sut = CreateSut();

        // Act & Assert
        sut.Tenant.ShouldBeNull();
    }

    [Fact]
    public void Tenant_Should_Return_Null_When_Missing_TenantName_Claim()
    {
        // Arrange
        SetupHttpContextWithClaims(
            new Claim(EksenClaims.TenantId, System.Ulid.NewUlid().ToString())
        );
        var sut = CreateSut();

        // Act & Assert
        sut.Tenant.ShouldBeNull();
    }

    [Fact]
    public void OriginalTenant_Should_Return_Null_When_No_HttpContext()
    {
        // Arrange
        _httpContextAccessorMock.Setup(a => a.HttpContext).Returns((HttpContext?)null);
        var sut = CreateSut();

        // Act & Assert
        sut.OriginalTenant.ShouldBeNull();
    }

    [Fact]
    public void OriginalTenant_Should_Return_AuthContextTenant_When_Valid_Claims()
    {
        // Arrange
        var originalTenantId = System.Ulid.NewUlid();
        SetupHttpContextWithClaims(
            new Claim(EksenClaims.OriginalTenantId, originalTenantId.ToString()),
            new Claim(EksenClaims.OriginalTenantName, "Original Corp")
        );
        var sut = CreateSut();

        // Act
        var originalTenant = sut.OriginalTenant;

        // Assert
        originalTenant.ShouldNotBeNull();
        originalTenant.TenantId.Value.ShouldBe(originalTenantId);
        originalTenant.TenantName.Value.ShouldBe("Original Corp");
    }

    [Fact]
    public void IsImpersonating_Should_Return_False_When_No_HttpContext()
    {
        // Arrange
        _httpContextAccessorMock.Setup(a => a.HttpContext).Returns((HttpContext?)null);
        var sut = CreateSut();

        // Act & Assert
        sut.IsImpersonating.ShouldBeFalse();
    }

    [Fact]
    public void IsImpersonating_Should_Return_True_When_Claim_Is_True()
    {
        // Arrange
        SetupHttpContextWithClaims(
            new Claim(EksenClaims.IsImpersonating, "true")
        );
        var sut = CreateSut();

        // Act & Assert
        sut.IsImpersonating.ShouldBeTrue();
    }

    [Fact]
    public void IsImpersonating_Should_Return_False_When_Claim_Is_False()
    {
        // Arrange
        SetupHttpContextWithClaims(
            new Claim(EksenClaims.IsImpersonating, "false")
        );
        var sut = CreateSut();

        // Act & Assert
        sut.IsImpersonating.ShouldBeFalse();
    }

    [Fact]
    public void IsImpersonating_Should_Return_False_When_No_Claim()
    {
        // Arrange
        SetupHttpContextWithClaims();
        var sut = CreateSut();

        // Act & Assert
        sut.IsImpersonating.ShouldBeFalse();
    }

    [Fact]
    public void IsImpersonating_Should_Return_False_When_Claim_Is_Not_Boolean()
    {
        // Arrange
        SetupHttpContextWithClaims(
            new Claim(EksenClaims.IsImpersonating, "not-a-bool")
        );
        var sut = CreateSut();

        // Act & Assert
        sut.IsImpersonating.ShouldBeFalse();
    }

    [Fact]
    public void UserType_Should_Return_Anonymous_When_Not_Authenticated()
    {
        // Arrange
        _httpContextAccessorMock.Setup(a => a.HttpContext).Returns((HttpContext?)null);
        var sut = CreateSut();

        // Act & Assert
        sut.UserType.ShouldBe(UserType.Anonymous);
    }

    [Fact]
    public void UserType_Should_Return_Tenant_When_Authenticated_With_Tenant()
    {
        // Arrange
        var userId = System.Ulid.NewUlid();
        var tenantId = System.Ulid.NewUlid();
        SetupHttpContextWithClaims(
            new Claim(_identityOptions.Value.ClaimsIdentity.UserIdClaimType, userId.ToString()),
            new Claim(_identityOptions.Value.ClaimsIdentity.UserNameClaimType, "Test User"),
            new Claim(_identityOptions.Value.ClaimsIdentity.EmailClaimType, "test@example.com"),
            new Claim(EksenClaims.TenantId, tenantId.ToString()),
            new Claim(EksenClaims.TenantName, "Test Tenant")
        );
        var sut = CreateSut();

        // Act & Assert
        sut.UserType.ShouldBe(UserType.Tenant);
    }

    [Fact]
    public void UserType_Should_Return_Host_When_Authenticated_Without_Tenant()
    {
        // Arrange
        var userId = System.Ulid.NewUlid();
        SetupHttpContextWithClaims(
            new Claim(_identityOptions.Value.ClaimsIdentity.UserIdClaimType, userId.ToString()),
            new Claim(_identityOptions.Value.ClaimsIdentity.UserNameClaimType, "Host User"),
            new Claim(_identityOptions.Value.ClaimsIdentity.EmailClaimType, "host@example.com")
        );
        var sut = CreateSut();

        // Act & Assert
        sut.UserType.ShouldBe(UserType.Host);
    }

    [Fact]
    public void IsHost_Should_Return_True_When_UserType_Is_Host()
    {
        // Arrange
        var userId = System.Ulid.NewUlid();
        SetupHttpContextWithClaims(
            new Claim(_identityOptions.Value.ClaimsIdentity.UserIdClaimType, userId.ToString()),
            new Claim(_identityOptions.Value.ClaimsIdentity.UserNameClaimType, "Host User"),
            new Claim(_identityOptions.Value.ClaimsIdentity.EmailClaimType, "host@example.com")
        );
        var sut = CreateSut();

        // Act & Assert
        sut.IsHost.ShouldBeTrue();
        sut.IsTenant.ShouldBeFalse();
    }

    [Fact]
    public void IsTenant_Should_Return_True_When_UserType_Is_Tenant()
    {
        // Arrange
        var userId = System.Ulid.NewUlid();
        var tenantId = System.Ulid.NewUlid();
        SetupHttpContextWithClaims(
            new Claim(_identityOptions.Value.ClaimsIdentity.UserIdClaimType, userId.ToString()),
            new Claim(_identityOptions.Value.ClaimsIdentity.UserNameClaimType, "Tenant User"),
            new Claim(_identityOptions.Value.ClaimsIdentity.EmailClaimType, "tenant@example.com"),
            new Claim(EksenClaims.TenantId, tenantId.ToString()),
            new Claim(EksenClaims.TenantName, "Test Tenant")
        );
        var sut = CreateSut();

        // Act & Assert
        sut.IsTenant.ShouldBeTrue();
        sut.IsHost.ShouldBeFalse();
    }
}
